// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Social;

/// <summary>
/// Injects OpenGraph, Twitter/X, canonical link, and Schema.org <c>TechArticle</c> JSON-LD metadata
/// into every page's <c>&lt;head&gt;</c> and emits deterministic <c>1200x630</c> SVG social preview cards
/// to <c>assets/social/&lt;slug&gt;.svg</c>.
/// </summary>
[System.Diagnostics.DebuggerDisplay("SocialPlugin: {Name}")]
public sealed class SocialPlugin : IBuildConfigurePlugin, IPagePostRenderPlugin, IBuildFinalizePlugin
{
    /// <summary>Tiebreak placing social tag injection after the theme shell and before <c>CspPlugin</c> (100) so JSON-LD scripts are hashed into strict CSP automatically.</summary>
    private const int PostRenderTiebreak = 40;

    /// <summary>Length of the <c>./</c> self-directory marker produced for root <c>index.md</c> in directory-URL mode.</summary>
    private const int SelfDirPrefixLength = 2;

    /// <summary>Queued SVG card render jobs keyed by card slug.</summary>
    private readonly ConcurrentDictionary<string, (byte[] Title, byte[] Description)> _cardJobs = new(StringComparer.Ordinal);

    /// <summary>Plugin options.</summary>
    private readonly SocialOptions _options;

    /// <summary>Normalized UTF-8 base URL ending with <c>/</c>, or empty when <c>site_url</c> is not configured.</summary>
    private byte[] _baseUrl = [];

    /// <summary>UTF-8 configured site name.</summary>
    private byte[] _siteName = [];

    /// <summary>UTF-8 configured site author.</summary>
    private byte[] _siteAuthor = [];

    /// <summary>Directory-URL flag captured from build configuration.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="SocialPlugin"/> class with default options.</summary>
    public SocialPlugin()
        : this(SocialOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SocialPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public SocialPlugin(in SocialOptions options) => _options = options;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "social"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <summary>Gets the closing <c>&lt;/head&gt;</c> tag bytes.</summary>
    private static ReadOnlySpan<byte> HeadClose => "</head>"u8;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _cardJobs.Clear();
        _baseUrl = NormalizeTrailingSlashUrl(context.SiteUrl);
        _siteName = context.SiteName;
        _siteAuthor = context.SiteAuthor;
        _useDirectoryUrls = context.UseDirectoryUrls;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        _options.Enabled && html.IndexOf(HeadClose) >= 0;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var html = context.Html;
        var headClose = html.IndexOf(HeadClose);
        if (!_options.Enabled || headClose < 0)
        {
            context.Output.Write(html);
            return;
        }

        var metadata = SocialMetadataExtractor.Extract(context.RelativePath, context.Source, _siteName);
        var canonicalUrl = ComposeCanonicalUrl(_baseUrl, context.RelativePath, _useDirectoryUrls);
        var imageUrl = ResolveImageUrl(metadata, _baseUrl, _options.GenerateSvgCards, out var generatedSvg);
        if (generatedSvg)
        {
            _ = _cardJobs.TryAdd(metadata.CardSlug, (metadata.Title, metadata.Description));
        }

        var payload = new SocialHeadPayload(
            metadata.Title,
            metadata.Description,
            canonicalUrl,
            imageUrl,
            generatedSvg,
            _siteName,
            _siteAuthor);

        context.Output.Write(html[..headClose]);
        SocialHeadWriter.WriteHeadTags(context.Output, html[..headClose], payload, _options);
        context.Output.Write(html[headClose..]);
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        if (_cardJobs.IsEmpty || context.OutputRoot.IsEmpty)
        {
            return;
        }

        var socialDir = (context.OutputRoot / "assets" / "social").Create();
        foreach (var entry in _cardJobs)
        {
            var svg = SocialSvgGenerator.RenderCard(
                entry.Value.Title,
                entry.Value.Description,
                _siteName,
                _baseUrl,
                _options);
            await socialDir.File($"{entry.Key}.svg").WriteAllBytesAsync(svg, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Composes the canonical absolute URL for <paramref name="relativePath"/>, or returns empty when <paramref name="baseUrl"/> is unset.</summary>
    /// <param name="baseUrl">Normalized trailing-slash site URL bytes.</param>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <param name="useDirectoryUrls">Directory-URL flag.</param>
    /// <returns>UTF-8 canonical URL bytes, or empty.</returns>
    internal static byte[] ComposeCanonicalUrl(
        ReadOnlySpan<byte> baseUrl,
        in FilePath relativePath,
        bool useDirectoryUrls)
    {
        if (baseUrl.IsEmpty)
        {
            return [];
        }

        var pageUrl = Utf8MarkdownUrl.FromRelativePath(relativePath, useDirectoryUrls).AsSpan();
        if (pageUrl.SequenceEqual("./"u8))
        {
            pageUrl = pageUrl[SelfDirPrefixLength..];
        }

        return [.. baseUrl, .. pageUrl];
    }

    /// <summary>Resolves the image URL for a page from either its frontmatter override or its generated SVG card path.</summary>
    /// <param name="metadata">Extracted page metadata.</param>
    /// <param name="baseUrl">Normalized site URL bytes.</param>
    /// <param name="generateSvgCards">Whether build-time SVG cards are enabled.</param>
    /// <param name="generatedSvg">Set to <c>true</c> when a generated SVG card is used.</param>
    /// <returns>UTF-8 image URL bytes, or empty when none applies.</returns>
    internal static byte[] ResolveImageUrl(
        in SocialPageMetadata metadata,
        ReadOnlySpan<byte> baseUrl,
        bool generateSvgCards,
        out bool generatedSvg)
    {
        var overrideImage = metadata.ImageOverride.AsSpan();
        if (!overrideImage.IsEmpty)
        {
            generatedSvg = false;
            if (overrideImage.StartsWith("https://"u8) || overrideImage.StartsWith("http://"u8) || baseUrl.IsEmpty)
            {
                return metadata.ImageOverride;
            }

            var trimmedRel = overrideImage.TrimStart((byte)'/');
            return [.. baseUrl, .. trimmedRel];
        }

        if (!generateSvgCards)
        {
            generatedSvg = false;
            return [];
        }

        generatedSvg = true;
        var slugBytes = Encoding.UTF8.GetBytes(metadata.CardSlug);
        return !baseUrl.IsEmpty
            ? [.. baseUrl, .. "assets/social/"u8, .. slugBytes, .. ".svg"u8]
            : [.. "/assets/social/"u8, .. slugBytes, .. ".svg"u8];
    }

    /// <summary>Trims whitespace from <paramref name="siteUrl"/> and ensures a single trailing slash when non-empty.</summary>
    /// <param name="siteUrl">Raw configured site URL bytes.</param>
    /// <returns>Normalized UTF-8 base URL or empty array.</returns>
    internal static byte[] NormalizeTrailingSlashUrl(ReadOnlySpan<byte> siteUrl)
    {
        var trimmed = siteUrl.Trim(" \t"u8);
        if (trimmed.IsEmpty)
        {
            return [];
        }

        return trimmed[^1] == (byte)'/' ? trimmed.ToArray() : [.. trimmed, (byte)'/'];
    }
}
