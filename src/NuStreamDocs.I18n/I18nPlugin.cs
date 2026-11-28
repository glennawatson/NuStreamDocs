// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.I18n;

/// <summary>
/// Multi-locale documentation plugin providing directory/suffix locale routing, missing-translation
/// fallback synthesis, <c>&lt;html lang&gt;</c> rewriting, <c>hreflang</c> head and <c>sitemap.xml</c>
/// alternate links, and an accessible language switcher bar.
/// </summary>
[System.Diagnostics.DebuggerDisplay("I18nPlugin: {Name}")]
public sealed class I18nPlugin :
    IBuildConfigurePlugin,
    IBuildDiscoverPlugin,
    IPagePostRenderPlugin,
    IBuildFinalizePlugin,
    IStaticAssetProvider,
    IHeadExtraProvider
{
    /// <summary>Post-render tiebreak running after theme shell and before <c>SocialPlugin</c> (40) and <c>CspPlugin</c> (100).</summary>
    private const int PostRenderTiebreak = 35;

    /// <summary>Finalize tiebreak running after <c>SitemapPlugin</c> writes <c>sitemap.xml</c>.</summary>
    private const int FinalizeTiebreak = 50;

    /// <summary>Maps logical locale-neutral paths to <c>(locale -&gt; relativePath)</c> entries discovered across the site.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, FilePath>> _routes = new(StringComparer.Ordinal);

    /// <summary>Case-insensitive set of configured locale codes.</summary>
    private readonly HashSet<string> _knownLocales;

    /// <summary>Plugin options.</summary>
    private readonly I18nOptions _options;

    /// <summary>Normalized trailing-slash site URL bytes (empty when <c>site_url</c> is unset).</summary>
    private byte[] _baseUrl = [];

    /// <summary>Directory-URL flag captured from build configuration.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="I18nPlugin"/> class with default options.</summary>
    public I18nPlugin()
        : this(I18nOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="I18nPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public I18nPlugin(in I18nOptions options)
    {
        _options = options;
        _knownLocales = [with(StringComparer.OrdinalIgnoreCase)];
        for (var i = 0; i < options.Locales.Length; i++)
        {
            _ = _knownLocales.Add(options.Locales[i].Code);
        }

        _ = _knownLocales.Add(options.DefaultLocale);
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "i18n"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => new(PluginBand.Late, FinalizeTiebreak);

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets =>
        _options.Enabled ? [(I18nStylesheet.AssetPath, I18nStylesheet.GetBytes())] : [];

    /// <summary>Gets the stylesheet link written into <c>&lt;head&gt;</c>.</summary>
    private static ReadOnlySpan<byte> StylesheetHeadLink =>
        "<link rel=\"stylesheet\" href=\"/assets/stylesheets/i18n.css\">\n"u8;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _routes.Clear();
        _baseUrl = NormalizeBaseUrl(context.SiteUrl);
        _useDirectoryUrls = context.UseDirectoryUrls;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || context.InputRoot.IsEmpty || !context.InputRoot.Exists())
        {
            return;
        }

        foreach (var file in context.InputRoot.EnumerateFiles("*.md", SearchOption.AllDirectories))
        {
            var rel = context.InputRoot.Relative(file);
            if (IsHiddenPath(rel.Value))
            {
                continue;
            }

            RecordPageRoute(rel);
        }

        if (!_options.FallbackToDefaultLocale || _options.Locales.Length <= 1)
        {
            return;
        }

        await SynthesizeMissingTranslationsAsync(context.InputRoot, context.SyntheticPages, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        _options.Enabled && (html.IndexOf("<html"u8) >= 0 || html.IndexOf("</head>"u8) >= 0);

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        if (!_options.Enabled)
        {
            context.Output.Write(context.Html);
            return;
        }

        var perLocale = RecordPageRoute(context.RelativePath, out var currentLocale, out _);
        var alternates = BuildAlternates(perLocale);
        I18nHtmlRewriter.Rewrite(
            context.Html,
            currentLocale,
            _options.DefaultLocale,
            alternates,
            _options,
            context.Output);
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.EnrichSitemap || _baseUrl.Length == 0 || context.OutputRoot.IsEmpty)
        {
            return;
        }

        var sitemapFile = context.OutputRoot.File("sitemap.xml");
        if (!sitemapFile.Exists())
        {
            return;
        }

        var lookup = BuildSitemapAlternateLookup();
        if (lookup.Count == 0)
        {
            return;
        }

        var existingBytes = await sitemapFile.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
        var enriched = I18nSitemapEnricher.Enrich(existingBytes, lookup, _options.DefaultLocale);
        await sitemapFile.WriteAllBytesAsync(enriched, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Trims whitespace and ensures a trailing slash on <paramref name="raw"/> when non-empty.</summary>
    /// <param name="raw">Raw site URL bytes.</param>
    /// <returns>Normalized base URL bytes, or empty.</returns>
    internal static byte[] NormalizeBaseUrl(ReadOnlySpan<byte> raw)
    {
        var trimmed = raw.Trim(" \t"u8);
        if (trimmed.IsEmpty)
        {
            return [];
        }

        return trimmed[^1] == (byte)'/' ? trimmed.ToArray() : [.. trimmed, (byte)'/'];
    }

    /// <summary>Prepends <paramref name="notice"/> after any YAML frontmatter block in <paramref name="source"/>.</summary>
    /// <param name="source">Default-locale Markdown source bytes.</param>
    /// <param name="notice">Optional untranslated notice Markdown bytes.</param>
    /// <returns>Composed fallback Markdown bytes.</returns>
    internal static byte[] PrependNoticePreservingFrontMatter(byte[] source, ReadOnlySpan<byte> notice)
    {
        if (notice.IsEmpty)
        {
            return source;
        }

        var span = source.AsSpan();
        if (span.StartsWith("---\n"u8) || span.StartsWith("---\r\n"u8))
        {
            var afterOpen = span.IndexOf((byte)'\n') + 1;
            var closeIdx = span[afterOpen..].IndexOf("\n---"u8);
            if (closeIdx >= 0)
            {
                var afterFence = afterOpen + closeIdx + 1;
                var endLine = span[afterFence..].IndexOf((byte)'\n');
                var split = endLine < 0 ? span.Length : afterFence + endLine + 1;
                return [.. span[..split], (byte)'\n', .. notice, .. "\n\n"u8, .. span[split..]];
            }
        }

        return [.. notice, .. "\n\n"u8, .. span];
    }

    /// <summary>Checks whether any segment of <paramref name="relative"/> begins with <c>.</c>.</summary>
    /// <param name="relative">Forward-slashed relative path.</param>
    /// <returns><c>true</c> for hidden files or dot-directories.</returns>
    private static bool IsHiddenPath(string relative) =>
        relative.StartsWith('.') || relative.Contains("/.", StringComparison.Ordinal);

    /// <summary>Registers <paramref name="relativePath"/> in the route table.</summary>
    /// <param name="relativePath">Page relative path.</param>
    private void RecordPageRoute(in FilePath relativePath) =>
        _ = RecordPageRoute(relativePath, out _, out _);

    /// <summary>Classifies and records <paramref name="relativePath"/> in the route table.</summary>
    /// <param name="relativePath">Page relative path.</param>
    /// <param name="locale">Resolved locale code.</param>
    /// <param name="logicalPath">Resolved logical path.</param>
    /// <returns>Locale dictionary for <paramref name="logicalPath"/>.</returns>
    private ConcurrentDictionary<string, FilePath> RecordPageRoute(
        in FilePath relativePath,
        out string locale,
        out string logicalPath)
    {
        LocaleRouteClassifier.Classify(relativePath, _knownLocales, _options.DefaultLocale, out locale, out logicalPath);
        var perLocale = _routes.GetOrAdd(logicalPath, static _ => new(StringComparer.OrdinalIgnoreCase));
        _ = perLocale.TryAdd(locale, relativePath);
        return perLocale;
    }

    /// <summary>Synthesizes fallback pages for configured locales lacking a translation of a default-locale page.</summary>
    /// <param name="inputRoot">Docs input root.</param>
    /// <param name="sink">Synthetic page sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async task.</returns>
    private async ValueTask SynthesizeMissingTranslationsAsync(
        DirectoryPath inputRoot,
        SyntheticPageSink sink,
        CancellationToken cancellationToken)
    {
        foreach (var routeEntry in _routes)
        {
            var perLocale = routeEntry.Value;
            if (!perLocale.TryGetValue(_options.DefaultLocale, out var defaultRelPath))
            {
                continue;
            }

            byte[]? cachedSource = null;
            for (var i = 0; i < _options.Locales.Length; i++)
            {
                var targetLocale = _options.Locales[i].Code;
                if (perLocale.ContainsKey(targetLocale))
                {
                    continue;
                }

                cachedSource ??= await inputRoot.File(defaultRelPath.Value).ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
                var fallbackBytes = PrependNoticePreservingFrontMatter(cachedSource, _options.UntranslatedNotice);
                var synthRel = LocaleRouteClassifier.ComposePrefixedPath(targetLocale, routeEntry.Key);
                _ = perLocale.TryAdd(targetLocale, synthRel);
                sink.Add(synthRel, fallbackBytes);
            }
        }
    }

    /// <summary>Builds the ordered <see cref="LocaleAlternateLink"/> array for <paramref name="perLocale"/>.</summary>
    /// <param name="perLocale">Locale-to-path map for one logical page.</param>
    /// <returns>Ordered alternate links.</returns>
    private LocaleAlternateLink[] BuildAlternates(ConcurrentDictionary<string, FilePath> perLocale)
    {
        List<LocaleAlternateLink> list = [];
        for (var i = 0; i < _options.Locales.Length; i++)
        {
            var def = _options.Locales[i];
            if (!perLocale.TryGetValue(def.Code, out var relPath))
            {
                continue;
            }

            var href = LocaleRouteClassifier.ComposeHref(relPath, _baseUrl, _useDirectoryUrls);
            list.Add(new(def.Code, def.DisplayName, href));
        }

        return [.. list];
    }

    /// <summary>Builds a lookup from canonical <c>&lt;loc&gt;</c> URL strings to their alternate locale arrays.</summary>
    /// <returns>Sitemap URL lookup table.</returns>
    private Dictionary<string, LocaleAlternateLink[]> BuildSitemapAlternateLookup()
    {
        Dictionary<string, LocaleAlternateLink[]> lookup = [with(StringComparer.Ordinal)];
        foreach (var entry in _routes)
        {
            var alternates = BuildAlternates(entry.Value);
            if (alternates.Length <= 1)
            {
                continue;
            }

            for (var i = 0; i < alternates.Length; i++)
            {
                lookup[Encoding.UTF8.GetString(alternates[i].Href)] = alternates;
            }
        }

        return lookup;
    }
}
