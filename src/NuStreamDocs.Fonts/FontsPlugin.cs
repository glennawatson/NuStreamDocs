// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Fonts.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Fonts;

/// <summary>Self-hosts the declared fonts: resolves them at build time and contributes the woff2 files, <c>fonts.css</c>, and the preload + stylesheet links.</summary>
public sealed class FontsPlugin : IBuildConfigurePlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>Weight a preload link targets.</summary>
    private const int PreloadWeight = 400;

    /// <summary>Number of hash bytes used in a content-addressed font filename.</summary>
    private const int FilenameHashBytes = 8;

    /// <summary>Glob matching the markdown source files scanned for <c>auto</c>-subset detection.</summary>
    private const string MarkdownGlob = "**/*.md";

    /// <summary>UTF-8 subset token meaning "derive the subsets from the rendered content".</summary>
    private static readonly byte[] AutoSubsetToken = [.. "auto"u8];

    /// <summary>UTF-8 subset token meaning "every subset the provider offers".</summary>
    private static readonly byte[] AllSubsetsToken = [.. "all"u8];

    /// <summary>Subsets requested for an <c>auto</c> face whose provider can't enumerate all subsets in one stylesheet.</summary>
    private static readonly byte[][] AutoFallbackSubsets = [[.. "latin"u8], [.. "latin-ext"u8]];

    /// <summary>The plugin's options.</summary>
    private readonly FontsOptions _options;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Static assets computed during configure.</summary>
    private (FilePath Path, byte[] Bytes)[] _staticAssets = [];

    /// <summary>Head-extra bytes computed during configure.</summary>
    private byte[] _headExtra = [];

    /// <summary>Initializes a new instance of the <see cref="FontsPlugin"/> class with default options.</summary>
    public FontsPlugin()
        : this(FontsOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public FontsPlugin(in FontsOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public FontsPlugin(in FontsOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "fonts"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => _staticAssets;

    /// <inheritdoc/>
    public async ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        if (_options.Faces is [] || !IsLastFontsPlugin(context.Plugins))
        {
            // When several FontsPlugin instances are registered — e.g. a Material theme registered its
            // default set and the site overrode it with UseFonts(...) — only the last one does the work.
            return;
        }

        var cache = new FontDownloadCache(_options.CacheDirectory, _options.Offline);
        var seenBlocks = AnyAutoFace() ? ScanInputForSeenBlocks(context.InputRoot) : null;
        Accumulator acc = new();
        for (var i = 0; i < _options.Faces.Length; i++)
        {
            await ProcessFaceAsync(_options.Faces[i], cache, context.InputRoot, seenBlocks, acc, cancellationToken)
                .ConfigureAwait(false);
        }

        ArrayBufferWriter<byte> cssSink = new();
        FontCssWriter.Write(CollectionsMarshal.AsSpan(acc.FaceCss), cssSink);
        var cssPath = StringCompose.Concat(_options.OutputSubdirectory.Value, "/fonts.css");
        acc.Assets.Add(((FilePath)cssPath, cssSink.WrittenSpan.ToArray()));
        _staticAssets = [.. acc.Assets];
        _headExtra = BuildHeadExtra(acc.Preload, cssPath);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            FontsLogging.LogStylesheetWritten(_logger, cssPath, _staticAssets.Length - 1);
        }
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer) => writer.Write(_headExtra);

    /// <summary>Returns the provider for the given kind.</summary>
    /// <param name="kind">Provider kind.</param>
    /// <returns>The provider instance.</returns>
    private static IFontProvider ProviderFor(FontProviderKind kind) => kind switch
    {
        FontProviderKind.Fontsource => FontsourceProvider.Instance,
        FontProviderKind.Local => LocalFontProvider.Instance,
        _ => GoogleFontProvider.Instance
    };

    /// <summary>Content-addresses <paramref name="bytes"/> into a short hex filename stem.</summary>
    /// <param name="bytes">The font file bytes.</param>
    /// <returns>A lowercase hex stem.</returns>
    private static string HashName(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes).AsSpan(0, FilenameHashBytes));

    /// <summary>Assembles the head-extra bytes: a preload link per preloaded face plus the stylesheet link.</summary>
    /// <param name="preload">Site-relative preload paths (UTF-8, without leading slash).</param>
    /// <param name="cssPath">Site-relative path to <c>fonts.css</c>.</param>
    /// <returns>The head-extra bytes.</returns>
    private static byte[] BuildHeadExtra(List<byte[]> preload, string cssPath)
    {
        ArrayBufferWriter<byte> sink = new();
        for (var i = 0; i < preload.Count; i++)
        {
            sink.Write("<link rel=\"preload\" as=\"font\" type=\"font/woff2\" crossorigin href=\"/"u8);
            sink.Write(preload[i]);
            sink.Write("\">\n"u8);
        }

        sink.Write("<link rel=\"stylesheet\" href=\"/"u8);
        sink.Write(Encoding.UTF8.GetBytes(cssPath));
        sink.Write("\">\n"u8);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Resolves a face's woff2 files, passing the usage bitset to the provider for <c>auto</c> Google faces so unused subsets are never downloaded.</summary>
    /// <param name="face">The declared face.</param>
    /// <param name="cache">Download cache.</param>
    /// <param name="inputRoot">Build input directory.</param>
    /// <param name="seenBlocks">Unicode-block bitset, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved resources.</returns>
    private static ValueTask<FontResource[]> ResolveForFaceAsync(
        FontFace face,
        FontDownloadCache cache,
        DirectoryPath inputRoot,
        bool[]? seenBlocks,
        CancellationToken cancellationToken)
    {
        var usage = IsAutoSubsets(face.Subsets) && face.Provider == FontProviderKind.Google ? seenBlocks : null;
        return ProviderFor(face.Provider)
            .ResolveAsync(face, RequestSubsetsFor(face), cache, inputRoot, usage, cancellationToken);
    }

    /// <summary>Returns the subset list to request from the provider for <paramref name="face"/> (mapping <c>auto</c> to "all" for Google, or to the safe default for other providers).</summary>
    /// <param name="face">The declared face.</param>
    /// <returns>The subset list to request.</returns>
    private static byte[][] RequestSubsetsFor(in FontFace face)
    {
        if (!IsAutoSubsets(face.Subsets))
        {
            return face.Subsets;
        }

        return face.Provider == FontProviderKind.Google ? [AllSubsetsToken] : AutoFallbackSubsets;
    }

    /// <summary>Returns whether <paramref name="subsets"/> is the single <c>auto</c> token.</summary>
    /// <param name="subsets">A face's subset list.</param>
    /// <returns><see langword="true"/> when it is <c>["auto"]</c>.</returns>
    private static bool IsAutoSubsets(byte[][] subsets) =>
        subsets is [var only] && only.AsSpan().SequenceEqual(AutoSubsetToken);

    /// <summary>Scans the markdown source files under <paramref name="inputRoot"/> for which Unicode blocks they touch.</summary>
    /// <param name="inputRoot">Build input directory.</param>
    /// <returns>A Unicode-block bitset (block 0 is always set).</returns>
    private static bool[] ScanInputForSeenBlocks(DirectoryPath inputRoot)
    {
        var seen = UnicodeRangeMatcher.NewSeenBlocks();
        if (inputRoot.IsEmpty || !Directory.Exists(inputRoot.Value))
        {
            return seen;
        }

        var matcher = new Matcher();
        matcher.AddInclude(MarkdownGlob);
        foreach (var file in matcher.Execute(new DirectoryInfoWrapper(new(inputRoot.Value))).Files)
        {
            var path = Path.Combine(inputRoot.Value, file.Path);
            if (File.Exists(path))
            {
                UnicodeRangeMatcher.MarkSeen(File.ReadAllBytes(path), seen);
            }
        }

        return seen;
    }

    /// <summary>Returns whether this instance is the last <see cref="FontsPlugin"/> registered on the builder.</summary>
    /// <param name="plugins">All registered plugins, in registration order.</param>
    /// <returns><see langword="true"/> when this instance is the last <see cref="FontsPlugin"/>.</returns>
    private bool IsLastFontsPlugin(IPlugin[] plugins)
    {
        for (var i = plugins.Length - 1; i >= 0; i--)
        {
            if (plugins[i] is FontsPlugin found)
            {
                return ReferenceEquals(found, this);
            }
        }

        return true;
    }

    /// <summary>Returns whether any declared face requests <c>auto</c> subsets.</summary>
    /// <returns><see langword="true"/> when at least one face uses <c>auto</c>.</returns>
    private bool AnyAutoFace()
    {
        for (var i = 0; i < _options.Faces.Length; i++)
        {
            if (IsAutoSubsets(_options.Faces[i].Subsets))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves one declared face, hashing its files into asset paths and collecting its CSS contribution and preload target.</summary>
    /// <param name="face">The declared face.</param>
    /// <param name="cache">Download cache.</param>
    /// <param name="inputRoot">Build input directory.</param>
    /// <param name="seenBlocks">Unicode-block bitset of the site's markdown content; <see langword="null"/> when no face requests <c>auto</c>.</param>
    /// <param name="acc">Accumulating build state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the face has been processed.</returns>
    private async Task ProcessFaceAsync(
        FontFace face,
        FontDownloadCache cache,
        DirectoryPath inputRoot,
        bool[]? seenBlocks,
        Accumulator acc,
        CancellationToken cancellationToken)
    {
        var familyName = Encoding.UTF8.GetString(face.FamilyBytes);
        var resources = await ResolveForFaceAsync(face, cache, inputRoot, seenBlocks, cancellationToken)
            .ConfigureAwait(false);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            FontsLogging.LogFontResolved(_logger, familyName, resources.Length);
        }

        var resourceCss = BuildResourceCss(face, resources, acc);
        var metrics = ReadMetrics(familyName, resources);
        acc.FaceCss.Add(
            new(
                face.Id,
                face.FamilyBytes,
                face.Display,
                face.Fallback,
                face.ThemeVariables,
                metrics,
                resourceCss));
    }

    /// <summary>Writes each resource's woff2 to the asset list, records the preload target, and returns the CSS rows.</summary>
    /// <param name="face">The declared face.</param>
    /// <param name="resources">The resolved files.</param>
    /// <param name="acc">Accumulating build state.</param>
    /// <returns>The CSS rows for this face.</returns>
    private FontCssWriter.ResourceCss[] BuildResourceCss(FontFace face, FontResource[] resources, Accumulator acc)
    {
        var rows = new FontCssWriter.ResourceCss[resources.Length];
        byte[]? preloadPath = null;
        for (var i = 0; i < resources.Length; i++)
        {
            var r = resources[i];
            var relativePath =
                StringCompose.Concat(_options.OutputSubdirectory.Value, "/", HashName(r.Woff2Bytes), ".woff2");
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            acc.Assets.Add(((FilePath)relativePath, r.Woff2Bytes));
            rows[i] = new(r.Weight, r.Style, r.UnicodeRange, pathBytes);
            if (face.Preload && preloadPath is null && r.Style == FontStyle.Normal && r.Weight == PreloadWeight)
            {
                preloadPath = pathBytes;
            }
        }

        if (preloadPath is not null)
        {
            acc.Preload.Add(preloadPath);
        }

        return rows;
    }

    /// <summary>Reads the CLS-fallback metrics from the first resource, logging a warning when unavailable.</summary>
    /// <param name="familyName">CSS family name (for the warning).</param>
    /// <param name="resources">The resolved files.</param>
    /// <returns>The metrics, or <see langword="null"/>.</returns>
    private FontMetrics? ReadMetrics(string familyName, FontResource[] resources)
    {
        var metrics = resources is [var first, ..] ? FontMetricsReader.Read(first.Woff2Bytes) : null;
        if (metrics is null && _logger.IsEnabled(LogLevel.Warning))
        {
            FontsLogging.LogMetricsUnavailable(_logger, familyName);
        }

        return metrics;
    }

    /// <summary>Mutable build state threaded through face processing.</summary>
    private sealed record Accumulator
    {
        /// <summary>Gets the static assets collected so far (font files; the stylesheet is added last).</summary>
        public List<(FilePath Path, byte[] Bytes)> Assets { get; } = [];

        /// <summary>Gets the per-face CSS inputs collected so far.</summary>
        public List<FontCssWriter.FaceCss> FaceCss { get; } = [];

        /// <summary>Gets the site-relative preload paths collected so far (UTF-8, without leading slash).</summary>
        public List<byte[]> Preload { get; } = [];
    }
}
