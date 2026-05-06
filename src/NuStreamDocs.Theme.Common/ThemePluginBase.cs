// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Encodings.Web;
using NuStreamDocs.Common;
using NuStreamDocs.Links;
using NuStreamDocs.Plugins;
using NuStreamDocs.Templating;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Shared page-shell implementation for the built-in themes.
/// </summary>
/// <typeparam name="TTheme">Loaded theme bundle type.</typeparam>
/// <typeparam name="TOptions">Theme option shape.</typeparam>
public abstract class ThemePluginBase<TTheme, TOptions>
    : IBuildConfigurePlugin, IBuildDiscoverPlugin, IPagePostRenderPlugin, IBuildFinalizePlugin
    where TTheme : class, IThemePackage
    where TOptions : struct, IThemeShellOptions
{
    /// <summary>Separators added when composing the normalized repo/edit prefix (<c>/</c> between segments and trailing <c>/</c>).</summary>
    private const int EditUrlSeparatorCount = 2;

    /// <summary>Directory separator string length for relative output path translation.</summary>
    private const int DirectorySeparatorLength = 1;

    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly TOptions _options;

    /// <summary>Output root captured during <see cref="ConfigureAsync"/>.</summary>
    private DirectoryPath _outputRoot;

    /// <summary>UTF-8 head-extras HTML assembled during <see cref="ConfigureAsync"/>; empty until it runs.</summary>
    private byte[] _headExtras = [];

    /// <summary>Plugin list captured during <see cref="ConfigureAsync"/> for static-asset composition at finalize time.</summary>
    private IPlugin[] _plugins = [];

    /// <summary>First registered nav-neighbour provider, captured during <see cref="ConfigureAsync"/>; null when none was registered.</summary>
    private INavNeighboursProvider? _neighbours;

    /// <summary>Per-build edit-link prefix; bytes ending in <c>/</c> ready to concatenate with a relative page path. Empty when edit links are disabled.</summary>
    private byte[] _editUrlPrefix = [];

    /// <summary>Short owner/repo label derived from the configured repo URL; empty when no repo URL is configured.</summary>
    private byte[] _repoLabel = [];

    /// <summary>Per-build canonical-URL prefix; empty when no site URL is configured.</summary>
    private byte[] _canonicalUrlPrefix = [];

    /// <summary>Effective favicon URL bytes — explicit option, then auto-discovered docs convention, then the theme's embedded default.</summary>
    private byte[] _resolvedFavicon = [];

    /// <summary>Effective copyright bytes with any <c>{year}</c> token expanded to the current year — computed once during configure.</summary>
    private byte[] _resolvedCopyright = [];

    /// <summary>Site-wide author captured from the configure context; used as the per-page <c>&lt;meta author&gt;</c> fallback when the page has no front-matter <c>author:</c>.</summary>
    private byte[] _siteAuthor = [];

    /// <summary>Whether the build emits pretty <c>foo/index.html</c> URLs.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Resolved asset-root bytes from the theme options; cached once at <see cref="ConfigureAsync"/> time.</summary>
    private byte[] _assetRoot = [];

    /// <summary>Initializes a new instance of the <see cref="ThemePluginBase{TTheme, TOptions}"/> class.</summary>
    /// <param name="options">Theme options.</param>
    /// <param name="theme">Loaded theme.</param>
    protected ThemePluginBase(in TOptions options, TTheme theme)
    {
        _options = options;
        LoadedTheme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    /// <inheritdoc/>
    public abstract ReadOnlySpan<byte> Name { get; }

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest);

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Latest);

    /// <summary>Gets the loaded theme.</summary>
    protected TTheme LoadedTheme { get; }

    /// <inheritdoc/>
    public ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return EnsureDefault404SourceAsync(context.InputRoot, TryReadThemeAsset);
    }

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _outputRoot = context.OutputRoot;
        _headExtras = HeadExtraComposer.Compose(context.Plugins);
        _plugins = context.Plugins;
        _neighbours = FindNavNeighboursProvider(context.Plugins);
        _editUrlPrefix = BuildEditUrlPrefix(_options.RepoUrl, _options.EditUri);
        _canonicalUrlPrefix = BuildCanonicalUrlPrefix(_options.SiteUrl);
        _repoLabel = BuildRepoLabel(_options.RepoUrl);
        _siteAuthor = context.SiteAuthor;
        _useDirectoryUrls = context.UseDirectoryUrls;
        _assetRoot = [.. _options.ResolveAssetRoot()];
        _resolvedCopyright = ExpandYearToken(_options.Copyright);
        _resolvedFavicon = ResolveFavicon(context.InputRoot);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => true;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var bodyLength = context.Html.Length;
        var bodyBuffer = ArrayPool<byte>.Shared.Rent(bodyLength);
        try
        {
            context.Html.CopyTo(bodyBuffer);

            var pageTitle = ResolvePageTitle(context.Source, context.RelativePath);
            var neighbours = ResolveNeighbours(context.RelativePath);
            TemplateData data = new(
                new(23, ByteArrayComparer.Instance)
                {
                    [ThemeShellBytes.LanguageKey] = _options.Language,
                    [ThemeShellBytes.SiteNameKey] = _options.SiteName,
                    [ThemeShellBytes.LogoKey] = ResolvePageRelativeUrl(_options.Logo, context.RelativePath, _useDirectoryUrls),
                    [ThemeShellBytes.SiteUrlKey] = _options.SiteUrl,
                    [ThemeShellBytes.CanonicalUrlKey] = ResolveCanonicalUrlBytes(context.RelativePath),
                    [ThemeShellBytes.SiteRootKey] = ThemeShellBytes.SiteRoot,
                    [ThemeShellBytes.PageTitleKey] = Utf8Encoder.Encode(pageTitle),
                    [ThemeShellBytes.BodyKey] = new(bodyBuffer, 0, bodyLength),
                    [ThemeShellBytes.AssetRootKey] = new([..ResolvePageRelativeAssetRoot(_assetRoot, context.RelativePath, _useDirectoryUrls)]),
                    [ThemeShellBytes.CopyrightKey] = _resolvedCopyright,
                    [ThemeShellBytes.RepoUrlKey] = _options.RepoUrl,
                    [ThemeShellBytes.RepoLabelKey] = _repoLabel,
                    [ThemeShellBytes.EditUrlKey] = ResolveEditUrlBytes(context.RelativePath),
                    [ThemeShellBytes.ScrollToTopKey] = _options.EnableScrollToTop ? ThemeShellBytes.Truthy : null,
                    [ThemeShellBytes.TocFollowKey] = _options.EnableTocFollow ? ThemeShellBytes.Truthy : null,
                    [ThemeShellBytes.PrevUrlKey] = ServedUrlBytes.FromPath(neighbours.PreviousPath, _useDirectoryUrls, leadingSlash: true),
                    [ThemeShellBytes.PrevTitleKey] = neighbours.PreviousTitle,
                    [ThemeShellBytes.NextUrlKey] = ServedUrlBytes.FromPath(neighbours.NextPath, _useDirectoryUrls, leadingSlash: true),
                    [ThemeShellBytes.NextTitleKey] = neighbours.NextTitle,
                    [ThemeShellBytes.HeadExtrasKey] = RewriteHeadExtraAssetHrefs(_headExtras, context.RelativePath, _useDirectoryUrls),
                    [ThemeShellBytes.DescriptionKey] = ResolveDescription(context.Source),
                    [ThemeShellBytes.HideNavigationKey] = ShouldHideNavigation(context.Source, context.RelativePath) ? ThemeShellBytes.Truthy : null,
                    [ThemeShellBytes.HideTocKey] = Yaml.FrontmatterValueExtractor.ListContains(context.Source, "hide"u8, "toc"u8) ? ThemeShellBytes.Truthy : null,
                    [ThemeShellBytes.GeneratorKey] = ThemeShellBytes.Generator,
                    [ThemeShellBytes.BuildDateKey] = ThemeShellBytes.BuildDate,
                    [ThemeShellBytes.FaviconKey] = _resolvedFavicon,
                    [ThemeShellBytes.AuthorKey] = ResolveAuthor(context.Source, _siteAuthor)
                },
                sections: null);

            LoadedTheme.Page.Render(data, LoadedTheme.Partials, context.Output);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bodyBuffer);
        }
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        var root = _outputRoot.IsEmpty ? context.OutputRoot : _outputRoot;
        if (root.IsEmpty)
        {
            return;
        }

        if (_options.WriteEmbeddedAssets)
        {
            var assets = LoadedTheme.StaticAssetEntries;
            for (var i = 0; i < assets.Length; i++)
            {
                var (relativePath, bytes) = assets[i];
                var target = Path.Combine(root, TranslateDirectorySeparators(relativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, bytes, cancellationToken).ConfigureAwait(false);
            }
        }

        StaticAssetComposer.WriteAll(_plugins, root);
    }

    /// <summary>Reads an embedded asset (relative to the theme's <c>Templates/</c> root) as UTF-8 bytes; <see langword="null"/> when the theme does not ship the asset.</summary>
    /// <param name="relativePath">Forward-slashed path under the theme's <c>Templates/</c>.</param>
    /// <returns>Asset bytes, or <see langword="null"/> when absent.</returns>
    protected virtual byte[]? TryReadThemeAsset(FilePath relativePath) => null;

    /// <summary>Probes <paramref name="inputRoot"/> for a conventional favicon file when the user hasn't configured one explicitly.</summary>
    /// <param name="inputRoot">Docs root.</param>
    /// <returns>Site-rooted UTF-8 URL bytes pointing at the discovered favicon, or an empty array.</returns>
    /// <remarks>Probes match mkdocs / mkdocs-material conventions in priority order.</remarks>
    private static byte[] DiscoverFavicon(DirectoryPath inputRoot)
    {
        if (inputRoot.IsEmpty)
        {
            return [];
        }

        ReadOnlySpan<string> candidates =
        [
            "images/favicons/favicon.ico",
            "images/favicons/favicon.svg",
            "images/favicon.ico",
            "images/favicon.svg",
            "assets/favicon.ico",
            "favicon.ico",
            "favicon.svg"
        ];

        for (var i = 0; i < candidates.Length; i++)
        {
            var probe = Path.Combine(inputRoot.Value, candidates[i]);
            if (File.Exists(probe))
            {
                return Utf8Concat.Concat("/"u8, System.Text.Encoding.UTF8.GetBytes(candidates[i].Replace('\\', '/')));
            }
        }

        return [];
    }

    /// <summary>Returns the first <see cref="INavNeighboursProvider"/> in <paramref name="plugins"/>, or null when none is registered.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>The provider, or null.</returns>
    private static INavNeighboursProvider? FindNavNeighboursProvider(IPlugin[] plugins)
    {
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is INavNeighboursProvider provider)
            {
                return provider;
            }
        }

        return null;
    }

    /// <summary>Resolves the page description — front-matter <c>description:</c>, or empty when absent.</summary>
    /// <param name="source">UTF-8 markdown source bytes.</param>
    /// <returns>UTF-8 description bytes, ready for direct emit; empty when no description.</returns>
    private static byte[] ResolveDescription(ReadOnlySpan<byte> source)
    {
        var raw = Yaml.FrontmatterValueExtractor.GetScalar(source, "description"u8);
        return raw.IsEmpty ? [] : [.. StripYamlQuotes(raw)];
    }

    /// <summary>Resolves the page author — front-matter <c>author:</c>, falling back to the site-wide value from the configure context.</summary>
    /// <param name="source">UTF-8 markdown source bytes.</param>
    /// <param name="siteAuthor">Site-wide author bytes captured at configure time; empty when not configured.</param>
    /// <returns>UTF-8 author bytes; empty when neither front-matter nor site_author is set.</returns>
    private static byte[] ResolveAuthor(ReadOnlySpan<byte> source, byte[] siteAuthor)
    {
        var raw = Yaml.FrontmatterValueExtractor.GetScalar(source, "author"u8);
        return !raw.IsEmpty ? [.. StripYamlQuotes(raw)] : siteAuthor;
    }

    /// <summary>Resolves the page title — front-matter <c>title:</c>, then the first markdown H1, then the file stem.</summary>
    /// <param name="source">UTF-8 markdown source bytes.</param>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <returns>HTML-encoded title text.</returns>
    private static string ResolvePageTitle(ReadOnlySpan<byte> source, FilePath relativePath)
    {
        var fromFrontMatter = Yaml.FrontmatterValueExtractor.GetScalar(source, "title"u8);
        if (!fromFrontMatter.IsEmpty)
        {
            var unquoted = StripYamlQuotes(fromFrontMatter);
            return HtmlEncoder.Default.Encode(System.Text.Encoding.UTF8.GetString(unquoted));
        }

        var firstHeading = Markdown.MarkdownH1Scanner.FindFirst(source);
        return !firstHeading.IsEmpty
            ? HtmlEncoder.Default.Encode(System.Text.Encoding.UTF8.GetString(firstHeading))
            : HtmlEncoder.Default.Encode(Path.GetFileNameWithoutExtension(relativePath));
    }

    /// <summary>Drops a single matching pair of leading/trailing single- or double-quote bytes from <paramref name="value"/>.</summary>
    /// <param name="value">UTF-8 candidate.</param>
    /// <returns>Unquoted slice or <paramref name="value"/> unchanged.</returns>
    private static ReadOnlySpan<byte> StripYamlQuotes(ReadOnlySpan<byte> value) =>
        value.Length >= 2
        && value[0] is (byte)'"' or (byte)'\''
        && value[^1] == value[0]
            ? value[1..^1]
            : value;

    /// <summary>Normalizes the repo/edit roots once per build for per-page edit URL composition.</summary>
    /// <param name="repoUrl">Configured UTF-8 repository URL.</param>
    /// <param name="editUri">Configured UTF-8 edit path inside the repository.</param>
    /// <returns>The normalized prefix bytes ending in <c>/</c>, or an empty array when edit links are disabled.</returns>
    private static byte[] BuildEditUrlPrefix(byte[] repoUrl, byte[] editUri)
    {
        if (editUri is [])
        {
            return [];
        }

        var edit = editUri.AsSpan();
        if (IsAbsoluteUrl(edit))
        {
            // Already a full URL — don't prepend RepoUrl, just normalize the trailing slash.
            var trimmed = edit.TrimEnd((byte)'/');
            var absoluteDst = new byte[trimmed.Length + 1];
            trimmed.CopyTo(absoluteDst);
            absoluteDst[^1] = (byte)'/';
            return absoluteDst;
        }

        if (repoUrl is [])
        {
            return [];
        }

        var repo = repoUrl.AsSpan().TrimEnd((byte)'/');
        var relEdit = edit.Trim((byte)'/');
        var dst = new byte[repo.Length + relEdit.Length + EditUrlSeparatorCount];
        repo.CopyTo(dst);
        dst[repo.Length] = (byte)'/';
        relEdit.CopyTo(dst.AsSpan(repo.Length + 1));
        dst[^1] = (byte)'/';
        return dst;
    }

    /// <summary>Replaces every literal <c>{year}</c> occurrence in <paramref name="copyright"/> with the current four-digit year.</summary>
    /// <param name="copyright">Configured copyright bytes (may be empty).</param>
    /// <returns>A fresh byte array with the token expanded; returns <paramref name="copyright"/> unchanged when no token is present.</returns>
    /// <remarks>Lets consumers write <c>"Copyright © {year} Acme Corp"u8</c> in <c>WithCopyright</c> and have the year stay correct without touching the build script each January.</remarks>
    private static byte[] ExpandYearToken(byte[] copyright)
    {
        if (copyright is null or [])
        {
            return [];
        }

        var token = ThemeShellBytes.YearToken.AsSpan();
        var src = copyright.AsSpan();
        if (src.IndexOf(token) < 0)
        {
            return copyright;
        }

        ArrayBufferWriter<byte> writer = new(copyright.Length);
        var year = ThemeShellBytes.CurrentYear.AsSpan();
        while (true)
        {
            var idx = src.IndexOf(token);
            if (idx < 0)
            {
                if (!src.IsEmpty)
                {
                    src.CopyTo(writer.GetSpan(src.Length));
                    writer.Advance(src.Length);
                }

                break;
            }

            if (idx > 0)
            {
                src[..idx].CopyTo(writer.GetSpan(idx));
                writer.Advance(idx);
            }

            year.CopyTo(writer.GetSpan(year.Length));
            writer.Advance(year.Length);
            src = src[(idx + token.Length)..];
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Returns true when <paramref name="value"/> begins with <c>http://</c> or <c>https://</c> (case-insensitive).</summary>
    /// <param name="value">UTF-8 candidate URL.</param>
    /// <returns>True for absolute http(s) URLs.</returns>
    private static bool IsAbsoluteUrl(ReadOnlySpan<byte> value) =>
        AsciiByteHelpers.StartsWithIgnoreAsciiCase(value, 0, "http://"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(value, 0, "https://"u8);

    /// <summary>Translates forward slashes in <paramref name="relativePath"/> to the active directory separator when needed.</summary>
    /// <param name="relativePath">Relative output path using forward slashes.</param>
    /// <returns>OS-native relative path.</returns>
    private static string TranslateDirectorySeparators(string relativePath) =>
        Path.DirectorySeparatorChar is '/'
            ? relativePath
            : string.Create(relativePath.Length * DirectorySeparatorLength, relativePath, static (dst, state) =>
            {
                for (var i = 0; i < state.Length; i++)
                {
                    dst[i] = state[i] is '/' ? Path.DirectorySeparatorChar : state[i];
                }
            });

    /// <summary>Counts the depth of the served URL for page-relative asset composition.</summary>
    /// <param name="relativePath">Source-relative page path (e.g. <c>guide/intro.md</c>).</param>
    /// <param name="useDirectoryUrls">
    /// True when non-index pages collapse to directory slugs (e.g. <c>guide/intro.md</c> serves at
    /// <c>/guide/intro/</c>); the served path is then one level deeper than the source path.
    /// </param>
    /// <returns>Number of <c>../</c> hops a relative href on this page needs to reach the site root.</returns>
    private static int PageDepth(FilePath relativePath, bool useDirectoryUrls)
    {
        ReadOnlySpan<char> path = relativePath;
        if (path is ['/', ..])
        {
            path = path[1..];
        }

        var depth = 0;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] is '/' or '\\')
            {
                depth++;
            }
        }

        // With directory URLs, a non-index page like guide/intro.md serves at /guide/intro/, so
        // the document is one directory deeper than the source path implies. Index pages already
        // collapse to their parent directory's URL, so depth is unchanged for those.
        if (useDirectoryUrls && !IsIndexPage(path))
        {
            depth++;
        }

        return depth;
    }

    /// <summary>True when <paramref name="path"/>'s filename is <c>index.md</c> (case-insensitive).</summary>
    /// <param name="path">Source-relative path span.</param>
    /// <returns>True when the page is an index file whose served URL is its parent directory.</returns>
    private static bool IsIndexPage(ReadOnlySpan<char> path)
    {
        var lastSep = path.LastIndexOfAny('/', '\\');
        var fileName = lastSep < 0 ? path : path[(lastSep + 1)..];
        return fileName.Equals("index.md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds the page-relative <c>../</c> prefix bytes for a page at the given depth.</summary>
    /// <param name="depth">Number of directory segments between the input root and the page.</param>
    /// <returns>Empty span at depth 0; otherwise <c>../</c> repeated <paramref name="depth"/> times.</returns>
    private static byte[] PageRelativePrefixBytes(int depth)
    {
        if (depth is 0)
        {
            return [];
        }

        const int SegmentLength = 3;
        var dst = new byte[depth * SegmentLength];
        for (var i = 0; i < depth; i++)
        {
            var off = i * SegmentLength;
            const int SecondCharOffset = 1;
            const int SeparatorOffset = 2;
            dst[off] = (byte)'.';
            dst[off + SecondCharOffset] = (byte)'.';
            dst[off + SeparatorOffset] = (byte)'/';
        }

        return dst;
    }

    /// <summary>Rewrites a site-root-absolute asset root (<c>/assets</c>) to be page-relative for the current page; leaves absolute http(s) URLs untouched.</summary>
    /// <param name="assetRoot">UTF-8 asset root from the theme options.</param>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <param name="useDirectoryUrls">True when non-index pages serve at directory URLs (one extra <c>../</c> hop).</param>
    /// <returns>Page-relative asset-root bytes, or the original bytes when the input is an absolute URL.</returns>
    private static ReadOnlySpan<byte> ResolvePageRelativeAssetRoot(ReadOnlySpan<byte> assetRoot, FilePath relativePath, bool useDirectoryUrls)
    {
        if (IsAbsoluteUrl(assetRoot))
        {
            return assetRoot;
        }

        // 404 pages are served by static hosts (and the dev server) for any not-found URL, so
        // relative paths break — emit site-rooted URLs that resolve regardless of the request path.
        if (Is404Page(relativePath))
        {
            return assetRoot is [(byte)'/', ..] ? assetRoot : SiteRooted(assetRoot);
        }

        // Strip a leading '/' so concatenation with the page-relative prefix doesn't produce a site-root absolute URL.
        var trimmed = assetRoot is [(byte)'/', ..] ? assetRoot[1..] : assetRoot;
        var prefix = PageRelativePrefixBytes(PageDepth(relativePath, useDirectoryUrls));
        if (prefix.Length is 0)
        {
            return trimmed.ToArray();
        }

        var dst = new byte[prefix.Length + trimmed.Length];
        prefix.AsSpan().CopyTo(dst);
        trimmed.CopyTo(dst.AsSpan(prefix.Length));
        return dst;
    }

    /// <summary>True when <paramref name="relativePath"/> is the conventional <c>404.md</c> at the site root.</summary>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <returns>True for the 404 page.</returns>
    private static bool Is404Page(FilePath relativePath) =>
        string.Equals(relativePath.Value, "404.md", StringComparison.OrdinalIgnoreCase);

    /// <summary>Prefixes <paramref name="path"/> with <c>/</c> to anchor it at the site root.</summary>
    /// <param name="path">UTF-8 path bytes.</param>
    /// <returns>A site-rooted byte array.</returns>
    private static byte[] SiteRooted(ReadOnlySpan<byte> path)
    {
        var dst = new byte[path.Length + 1];
        dst[0] = (byte)'/';
        path.CopyTo(dst.AsSpan(1));
        return dst;
    }

    /// <summary>Rewrites a configured href (site-root absolute <c>/foo</c> or relative <c>foo</c>) to be page-relative for the current page; leaves absolute http(s) URLs untouched.</summary>
    /// <param name="href">UTF-8 href from the theme options.</param>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <param name="useDirectoryUrls">True when non-index pages serve at directory URLs (one extra <c>../</c> hop).</param>
    /// <returns>Page-relative href bytes; the original bytes when the input is empty or an absolute URL.</returns>
    private static byte[] ResolvePageRelativeUrl(byte[] href, FilePath relativePath, bool useDirectoryUrls)
    {
        if (href is [] || IsAbsoluteUrl(href))
        {
            return href;
        }

        if (Is404Page(relativePath))
        {
            return href is [(byte)'/', ..] ? href : SiteRooted(href);
        }

        var span = href.AsSpan();
        var trimmed = span is [(byte)'/', ..] ? span[1..] : span;
        var prefix = PageRelativePrefixBytes(PageDepth(relativePath, useDirectoryUrls));
        if (prefix.Length is 0)
        {
            return trimmed.ToArray();
        }

        var dst = new byte[prefix.Length + trimmed.Length];
        prefix.AsSpan().CopyTo(dst);
        trimmed.CopyTo(dst.AsSpan(prefix.Length));
        return dst;
    }

    /// <summary>Rewrites every <c>"/assets/</c> / <c>'/assets/</c> href in the cached head-extras blob to be page-relative for the current page.</summary>
    /// <param name="headExtras">UTF-8 head-extras HTML composed once per build.</param>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <param name="useDirectoryUrls">True when non-index pages serve at directory URLs (one extra <c>../</c> hop).</param>
    /// <returns>Rewritten bytes; the original blob when no occurrences match or the page is at depth 0 and only the leading slash needs trimming.</returns>
    private static byte[] RewriteHeadExtraAssetHrefs(byte[] headExtras, FilePath relativePath, bool useDirectoryUrls)
    {
        if (headExtras is [])
        {
            return headExtras;
        }

        var source = headExtras.AsSpan();
        if (source.IndexOf("/assets/"u8) < 0)
        {
            return headExtras;
        }

        if (Is404Page(relativePath))
        {
            return headExtras;
        }

        var prefix = PageRelativePrefixBytes(PageDepth(relativePath, useDirectoryUrls));
        ArrayBufferWriter<byte> writer = new(headExtras.Length);
        var cursor = 0;
        while (cursor < source.Length)
        {
            var rest = source[cursor..];
            var idx = rest.IndexOf("/assets/"u8);
            if (idx < 0)
            {
                writer.Write(rest);
                break;
            }

            // Only rewrite hrefs preceded by a quote — leaves CSS url(/assets/...) inside <style> blocks untouched only if they exist (none today).
            var absolute = cursor + idx;
            if (absolute > 0 && source[absolute - 1] is (byte)'"' or (byte)'\'')
            {
                writer.Write(rest[..idx]);
                writer.Write(prefix);

                // Drop the leading '/'; emit "assets/" then continue past the matched separator.
                writer.Write("assets/"u8);
                cursor = absolute + "/assets/"u8.Length;
                continue;
            }

            // No quote in front; copy through up to and including the match and keep scanning.
            writer.Write(rest[..(idx + "/assets/"u8.Length)]);
            cursor = absolute + "/assets/"u8.Length;
        }

        return [.. writer.WrittenSpan];
    }

    /// <summary>Returns the site URL trimmed and slash-suffixed for canonical-URL composition.</summary>
    /// <param name="siteUrl">Configured UTF-8 absolute site URL.</param>
    /// <returns>Bytes ending in <c>/</c>, or an empty array when no site URL is configured.</returns>
    private static byte[] BuildCanonicalUrlPrefix(byte[] siteUrl)
    {
        if (siteUrl is [])
        {
            return [];
        }

        var trimmed = siteUrl.AsSpan().TrimEnd((byte)'/');
        var dst = new byte[trimmed.Length + 1];
        trimmed.CopyTo(dst);
        dst[^1] = (byte)'/';
        return dst;
    }

    /// <summary>Returns the short repo label for display next to the source icon.</summary>
    /// <param name="repoUrl">Configured UTF-8 repository URL.</param>
    /// <returns>
    /// The last two non-empty path segments of <paramref name="repoUrl"/> joined with <c>/</c>; the full URL
    /// when fewer than two segments are present; an empty array when <paramref name="repoUrl"/> is empty.
    /// </returns>
    private static byte[] BuildRepoLabel(byte[] repoUrl)
    {
        if (repoUrl is [])
        {
            return [];
        }

        var span = repoUrl.AsSpan().TrimEnd((byte)'/');
        if (span.IsEmpty)
        {
            return [.. repoUrl];
        }

        var lastSlash = span.LastIndexOf((byte)'/');
        if (lastSlash < 0)
        {
            return [.. repoUrl];
        }

        var head = span[..lastSlash];
        var tail = span[(lastSlash + 1)..];
        var prevSlash = head.LastIndexOf((byte)'/');
        var owner = prevSlash < 0 ? head : head[(prevSlash + 1)..];
        if (owner.IsEmpty || tail.IsEmpty)
        {
            return [.. repoUrl];
        }

        var dst = new byte[owner.Length + 1 + tail.Length];
        owner.CopyTo(dst);
        dst[owner.Length] = (byte)'/';
        tail.CopyTo(dst.AsSpan(owner.Length + 1));
        return dst;
    }

    /// <summary>Returns the URL slug for the page, matching the build pipeline's emit shape.</summary>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="useDirectoryUrls">Whether non-index pages collapse to directory slugs.</param>
    /// <returns>The slug span (no leading <c>/</c>); empty when the page is the root <c>index</c>.</returns>
    private static ReadOnlySpan<char> ToCanonicalSlug(FilePath relativePath, bool useDirectoryUrls)
    {
        ReadOnlySpan<char> path = relativePath;
        if (path is ['/', ..])
        {
            path = path[1..];
        }

        const string MdExt = ".md";
        const string IndexStem = "index.md";

        if (path.EndsWith(IndexStem, StringComparison.OrdinalIgnoreCase))
        {
            return path[..^IndexStem.Length];
        }

        if (!path.EndsWith(MdExt, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var stem = path[..^MdExt.Length];
        if (useDirectoryUrls)
        {
            var dst = new char[stem.Length + 1];
            stem.CopyTo(dst);
            dst[^1] = '/';
            return dst;
        }

        var html = new char[stem.Length + ".html".Length];
        stem.CopyTo(html);
        ".html".CopyTo(html.AsSpan(stem.Length));
        return html;
    }

    /// <summary>Writes a default <c>404.md</c> source page from the theme's embedded <c>Templates/404.md</c> when the user hasn't authored one.</summary>
    /// <param name="inputRoot">Absolute docs root.</param>
    /// <param name="readThemeAsset">Per-theme embedded-resource reader.</param>
    /// <returns>Async task.</returns>
    private static ValueTask EnsureDefault404SourceAsync(DirectoryPath inputRoot, Func<FilePath, byte[]?> readThemeAsset)
    {
        if (inputRoot.IsEmpty || !Directory.Exists(inputRoot.Value))
        {
            return ValueTask.CompletedTask;
        }

        var target = Path.Combine(inputRoot.Value, "404.md");
        if (File.Exists(target))
        {
            return ValueTask.CompletedTask;
        }

        var body = readThemeAsset((FilePath)"404.md");
        return body is null ? ValueTask.CompletedTask : new(File.WriteAllBytesAsync(target, body));
    }

    /// <summary>Picks the effective favicon URL: explicit option → docs-tree probe → theme-bundled default → empty.</summary>
    /// <param name="inputRoot">Docs root passed through to the discovery probe.</param>
    /// <returns>UTF-8 URL bytes the page template renders into the <c>&lt;link rel="icon"&gt;</c> attribute.</returns>
    private byte[] ResolveFavicon(DirectoryPath inputRoot)
    {
        if (_options.Favicon is [_, ..])
        {
            return _options.Favicon;
        }

        var discovered = DiscoverFavicon(inputRoot);
        if (discovered is [_, ..])
        {
            return discovered;
        }

        var defaultRelative = _options.DefaultEmbeddedFaviconRelativeUrl;
        return defaultRelative is [_, ..] && _options.WriteEmbeddedAssets
            ? Utf8Concat.Concat(_assetRoot, defaultRelative)
            : [];
    }

    /// <summary>Returns true when the rendered page should hide the primary sidebar.</summary>
    /// <param name="source">UTF-8 markdown source bytes.</param>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <returns>True when the sidebar should be hidden.</returns>
    /// <remarks>Honours the page's <c>hide: [navigation]</c> frontmatter and the nav provider's hint for empty sidebars.</remarks>
    private bool ShouldHideNavigation(ReadOnlySpan<byte> source, FilePath relativePath) =>
        Yaml.FrontmatterValueExtractor.ListContains(source, "hide"u8, "navigation"u8)
        || (_neighbours?.ShouldHidePrimarySidebar(relativePath) ?? false);

    /// <summary>Resolves prev/next neighbours according to the configured footer settings.</summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>The neighbours; <see cref="NavNeighbours.None"/> when the footer is disabled or no provider is registered.</returns>
    private NavNeighbours ResolveNeighbours(FilePath relativePath)
    {
        if (!_options.EnableNavigationFooter || _neighbours is null)
        {
            return NavNeighbours.None;
        }

        if (_options.SectionScopedFooter)
        {
            return _neighbours.GetSectionNeighbours(relativePath);
        }

        return _neighbours.GetNeighbours(relativePath);
    }

    /// <summary>Returns the canonical URL for the page; empty when no site URL is configured.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <returns>The canonical URL bytes, or an empty array.</returns>
    private byte[] ResolveCanonicalUrlBytes(FilePath relativePath)
    {
        if (_canonicalUrlPrefix is [])
        {
            return [];
        }

        var slug = ToCanonicalSlug(relativePath, _useDirectoryUrls);
        if (slug.Length is 0)
        {
            return _canonicalUrlPrefix;
        }

        var dst = new byte[_canonicalUrlPrefix.Length + slug.Length];
        _canonicalUrlPrefix.AsSpan().CopyTo(dst);
        var write = _canonicalUrlPrefix.Length;
        for (var i = 0; i < slug.Length; i++)
        {
            // Source-relative paths from the build pipeline are ASCII-safe; Windows back-slashes fold to '/'.
            dst[write++] = slug[i] is '\\' ? (byte)'/' : (byte)slug[i];
        }

        return dst;
    }

    /// <summary>Builds the per-page edit URL from the configured repo URL + edit prefix + the page's relative path.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <returns>The edit URL bytes, or an empty array when not configured.</returns>
    private byte[] ResolveEditUrlBytes(FilePath relativePath)
    {
        if (_editUrlPrefix is [])
        {
            return [];
        }

        ReadOnlySpan<char> path = relativePath;
        var start = path is ['/', ..] ? 1 : 0;
        var dst = new byte[_editUrlPrefix.Length + path.Length - start];
        _editUrlPrefix.AsSpan().CopyTo(dst);
        var write = _editUrlPrefix.Length;
        for (var i = start; i < path.Length; i++)
        {
            // Relative paths from the build pipeline are already ASCII-safe (a-z, 0-9, -, _, /, .) — back-slash on Windows folds to '/'.
            dst[write++] = path[i] is '\\' ? (byte)'/' : (byte)path[i];
        }

        return dst;
    }
}
