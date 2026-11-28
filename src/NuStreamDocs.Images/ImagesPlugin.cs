// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Images;

/// <summary>
/// Rewrites rendered <c>&lt;img&gt;</c> tags with intrinsic <c>width</c>/<c>height</c> attributes read from local
/// PNG, JPEG, GIF, WebP, AVIF, and SVG headers, adds <c>loading="lazy"</c> and <c>decoding="async"</c>,
/// and wraps titled images in <c>&lt;figure&gt;</c>/<c>&lt;figcaption&gt;</c> markup.
/// </summary>
[System.Diagnostics.DebuggerDisplay("ImagesPlugin: {Name}")]
public sealed class ImagesPlugin : IBuildConfigurePlugin, IPagePostRenderPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>Tiebreak running before <c>LightboxPlugin</c> (<c>PluginBand.Late</c>) and <c>AuditPlugin</c> so injected dimensions satisfy zero-CLS audits.</summary>
    private const int PostRenderTiebreak = 50;

    /// <summary>Maximum header bytes read from disk for dimension extraction.</summary>
    private const int MaxHeaderProbeBytes = 16_384;

    /// <summary>Per-build cache of resolved image dimensions keyed by normalized full file path.</summary>
    private readonly ConcurrentDictionary<string, ImageDimensions?> _dimensionCache = new(StringComparer.Ordinal);

    /// <summary>Plugin options.</summary>
    private readonly ImagesOptions _options;

    /// <summary>Docs input root directory captured at configure time.</summary>
    private DirectoryPath _inputRoot;

    /// <summary>Initializes a new instance of the <see cref="ImagesPlugin"/> class with default options.</summary>
    public ImagesPlugin()
        : this(ImagesOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ImagesPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public ImagesPlugin(in ImagesOptions options) => _options = options;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "images"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Normal, PostRenderTiebreak);

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets =>
        _options.Enabled ? [(ImagesStylesheet.AssetPath, ImagesStylesheet.GetBytes())] : [];

    /// <summary>Gets the stylesheet link tag written into <c>&lt;head&gt;</c>.</summary>
    private static ReadOnlySpan<byte> StylesheetHeadLink =>
        "<link rel=\"stylesheet\" href=\"/assets/stylesheets/images.css\">\n"u8;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _inputRoot = context.InputRoot;
        _dimensionCache.Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        _options.Enabled && html.IndexOf("<img "u8) >= 0;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        if (!_options.Enabled || context.Html.IndexOf("<img "u8) < 0)
        {
            context.Output.Write(context.Html);
            return;
        }

        var pageDir = context.RelativePath.Directory;
        ImagesHtmlRewriter.Rewrite(
            context.Html,
            _options,
            srcBytes => ResolveDimensions(srcBytes, pageDir),
            context.Output);
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        if (!_options.Enabled)
        {
            return;
        }

        writer.Write(StylesheetHeadLink);
    }

    /// <summary>Reads at most <see cref="MaxHeaderProbeBytes"/> from <paramref name="filePath"/> and parses dimensions.</summary>
    /// <param name="filePath">Full path to the candidate image file.</param>
    /// <returns>Parsed <see cref="ImageDimensions"/>, or <c>null</c> when missing or unrecognized.</returns>
    internal static ImageDimensions? ProbeFileDimensions(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        using var stream = File.OpenRead(filePath);
        var toRead = (int)Math.Min(stream.Length, MaxHeaderProbeBytes);
        var buffer = new byte[toRead];
        var totalRead = stream.ReadAtLeast(buffer, toRead, throwOnEndOfStream: false);
        return ImageHeaderReader.TryReadDimensions(buffer.AsSpan(0, totalRead), out var dims) ? dims : null;
    }

    /// <summary>
    /// Resolves a relative or root-relative <c>src</c> value against <paramref name="inputRoot"/> and
    /// <paramref name="pageDir"/>, rejecting traversal outside <paramref name="inputRoot"/>.
    /// </summary>
    /// <param name="inputRoot">Docs input root directory.</param>
    /// <param name="pageDir">Page-relative directory within <paramref name="inputRoot"/>.</param>
    /// <param name="srcBytes">UTF-8 <c>src</c> attribute bytes.</param>
    /// <returns>Canonical full path inside <paramref name="inputRoot"/>, or <c>null</c> when unresolvable or outside root.</returns>
    internal static string? ResolveCandidateFilePath(
        in DirectoryPath inputRoot,
        in DirectoryPath pageDir,
        ReadOnlySpan<byte> srcBytes)
    {
        if (inputRoot.IsEmpty)
        {
            return null;
        }

        var cleanSpan = StripQueryAndFragment(srcBytes);
        if (cleanSpan.IsEmpty)
        {
            return null;
        }

        var decoded = Uri.UnescapeDataString(Encoding.UTF8.GetString(cleanSpan));
        var normalizedRel = decoded.TrimStart('/');
        var baseDir = decoded.StartsWith('/') || pageDir.IsEmpty
            ? inputRoot.Value
            : Path.Combine(inputRoot.Value, pageDir.Value);

        var fullRoot = Path.GetFullPath(inputRoot.Value);
        var fullCandidate = Path.GetFullPath(Path.Combine(baseDir, normalizedRel));
        return fullCandidate.StartsWith(fullRoot, StringComparison.Ordinal) ? fullCandidate : null;
    }

    /// <summary>Strips <c>?query</c> and <c>#fragment</c> suffixes from a URI byte span.</summary>
    /// <param name="src">Raw <c>src</c> bytes.</param>
    /// <returns>Path portion only.</returns>
    private static ReadOnlySpan<byte> StripQueryAndFragment(ReadOnlySpan<byte> src)
    {
        var query = src.IndexOfAny((byte)'?', (byte)'#');
        return query < 0 ? src : src[..query];
    }

    /// <summary>Resolves cached dimensions for <paramref name="srcBytes"/> relative to <paramref name="pageDir"/>.</summary>
    /// <param name="srcBytes">UTF-8 <c>src</c> bytes.</param>
    /// <param name="pageDir">Page directory relative to input root.</param>
    /// <returns>Cached <see cref="ImageDimensions"/> or <c>null</c>.</returns>
    private ImageDimensions? ResolveDimensions(byte[] srcBytes, in DirectoryPath pageDir)
    {
        var resolvedPath = ResolveCandidateFilePath(_inputRoot, pageDir, srcBytes);
        return resolvedPath is null
            ? null
            : _dimensionCache.GetOrAdd(resolvedPath, ProbeFileDimensions);
    }
}
