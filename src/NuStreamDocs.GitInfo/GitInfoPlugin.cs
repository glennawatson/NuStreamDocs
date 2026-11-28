// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.GitInfo;

/// <summary>
/// Reads batched Git revision dates and contributor names once at configure time,
/// injects revision footers and <c>dcterms.modified</c> meta tags into rendered
/// pages, and enriches <c>sitemap.xml</c> with <c>&lt;lastmod&gt;</c> elements.
/// </summary>
[System.Diagnostics.DebuggerDisplay("GitInfoPlugin: {Name}")]
public sealed class GitInfoPlugin
    : IBuildConfigurePlugin,
        IPagePostRenderPlugin,
        IBuildFinalizePlugin,
        IStaticAssetProvider,
        IHeadExtraProvider
{
    /// <summary>Tiebreak running after theme shell wrapping and before CSP hashing.</summary>
    private const int PostRenderTiebreak = 50;

    /// <summary>Stylesheet asset path relative to output root.</summary>
    private static readonly FilePath CssAssetPath = new("assets/extensions/git-info.css");

    /// <summary>Cached stylesheet asset tuple when enabled.</summary>
    private static readonly (FilePath Path, byte[] Bytes)[] EnabledAssets =
        [(CssAssetPath, [.. DefaultStylesheetBytes])];

    /// <summary>Collected URL-to-date pairs for <c>sitemap.xml</c> enrichment.</summary>
    private readonly ConcurrentQueue<(byte[] Url, byte[] IsoDate)> _urlDates = [];

    /// <summary>Configured options.</summary>
    private readonly GitInfoOptions _options;

    /// <summary>Parsed Git history map keyed by forward-slashed relative path.</summary>
    private Dictionary<byte[], GitPageMetadata> _gitMap = [with(ByteArrayComparer.Instance)];

    /// <summary>Normalized site URL ending with <c>/</c>, or <c>null</c> when unconfigured.</summary>
    private byte[]? _baseUrlBytes;

    /// <summary>Whether directory URLs are active.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="GitInfoPlugin"/> class with <see cref="GitInfoOptions.Default"/>.</summary>
    public GitInfoPlugin()
        : this(GitInfoOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GitInfoPlugin"/> class.</summary>
    /// <param name="options">Plugin configuration.</param>
    public GitInfoPlugin(in GitInfoOptions options) => _options = options;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "git-info"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => new(PluginBand.Latest);

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets =>
        _options.IncludeStylesheet ? EnabledAssets : [];

    /// <summary>Gets the head stylesheet link tag.</summary>
    private static ReadOnlySpan<byte> HeadCssLink =>
        "<link rel=\"stylesheet\" href=\"/assets/extensions/git-info.css\">\n"u8;

    /// <summary>Gets the default responsive footer CSS rules.</summary>
    private static ReadOnlySpan<byte> DefaultStylesheetBytes =>
        ".nd-git-info{display:flex;flex-wrap:wrap;gap:.75rem 1.5rem;margin-top:2.5rem;padding-top:1rem;border-top:1px solid rgba(127,127,127,.25);font-size:.8125rem;opacity:.82}\n"u8;

    /// <inheritdoc/>
    public async ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _urlDates.Clear();
        _useDirectoryUrls = context.UseDirectoryUrls;
        _baseUrlBytes = NormalizeBaseUrl(context.SiteUrl);

        var provider = _options.LogProvider ?? ProcessGitLogProvider.Instance;
        var logBytes = await provider.ReadGitLogAsync(context.InputRoot, cancellationToken).ConfigureAwait(false);
        _gitMap = GitLogParser.ParseLog(logBytes, _options.MaxContributors);
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        !html.IsEmpty && (_options.EmitFooter || _options.EmitMetaTag || _options.EnrichSitemap);

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        if (!GitLogParser.TryResolvePage(
            context.RelativePath,
            context.Source,
            _gitMap,
            _options.MaxContributors,
            out var metadata))
        {
            context.Output.Write(context.Html);
            return;
        }

        if (_options.EnrichSitemap)
        {
            var url = Utf8MarkdownUrl.FromRelativePath(context.RelativePath, _useDirectoryUrls);
            _urlDates.Enqueue((url, metadata.LastUpdatedIsoDate));
        }

        GitInfoHtmlRewriter.RewritePage(context.Html, metadata, _options, context.Output);
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        if (!_options.EnrichSitemap || _baseUrlBytes is null || _urlDates.IsEmpty)
        {
            return;
        }

        var sitemapFile = context.OutputRoot.File("sitemap.xml");
        if (!sitemapFile.Exists())
        {
            return;
        }

        Dictionary<byte[], byte[]> dateMap = [with(ByteArrayComparer.Instance)];
        foreach (var item in _urlDates)
        {
            dateMap[item.Url] = item.IsoDate;
        }

        var rawSitemap = await sitemapFile.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
        var enriched = GitInfoHtmlRewriter.EnrichSitemapBytes(rawSitemap, _baseUrlBytes, dateMap);
        await sitemapFile.WriteAllBytesAsync(enriched, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        if (!_options.IncludeStylesheet)
        {
            return;
        }

        writer.Write(HeadCssLink);
    }

    /// <summary>Normalizes <paramref name="raw"/> to a UTF-8 base URL ending with <c>/</c>, or <c>null</c> when blank.</summary>
    /// <param name="raw">Raw site URL bytes.</param>
    /// <returns>Normalized base URL or <c>null</c>.</returns>
    internal static byte[]? NormalizeBaseUrl(ReadOnlySpan<byte> raw)
    {
        var trimmed = raw.Trim(" \t"u8);
        if (trimmed.IsEmpty)
        {
            return null;
        }

        return trimmed[^1] is (byte)'/' ? [.. trimmed] : [.. trimmed, (byte)'/'];
    }
}
