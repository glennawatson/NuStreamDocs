// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Encodings.Web;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Templating;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Shared page-shell implementation for the built-in themes.
/// </summary>
/// <typeparam name="TTheme">Loaded theme bundle type.</typeparam>
/// <typeparam name="TOptions">Theme option shape.</typeparam>
public abstract class ThemePluginBase<TTheme, TOptions> : IDocPlugin
    where TTheme : class, IThemePackage
    where TOptions : struct, IThemeShellOptions
{
    /// <summary>Markdown extension stripped when computing served URLs.</summary>
    private const string MarkdownExtension = ".md";

    /// <summary>Characters added when replacing <c>.md</c> with <c>.html</c>.</summary>
    private const int HtmlExtensionGrowth = 2;

    /// <summary>Separators added when composing the normalized repo/edit prefix (<c>/</c> between segments and trailing <c>/</c>).</summary>
    private const int EditUrlSeparatorCount = 2;

    /// <summary>Directory separator string length for relative output path translation.</summary>
    private const int DirectorySeparatorLength = 1;

    /// <summary>Root-relative site URL used by the templates.</summary>
    private static readonly byte[] SiteRootBytes = [.. "/"u8];

    /// <summary>Template truthy flag emitted for enabled boolean options.</summary>
    private static readonly byte[] TruthyBytes = [.. "1"u8];

    /// <summary>UTF-8 template-data key for <c>language</c>.</summary>
    private static readonly byte[] LanguageKey = "language"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>site_name</c>.</summary>
    private static readonly byte[] SiteNameKey = "site_name"u8.ToArray();

    /// <summary>UTF-8 template variable for the absolute site URL.</summary>
    private static readonly byte[] SiteUrlKey = "site_url"u8.ToArray();

    /// <summary>UTF-8 template variable for the per-page canonical URL.</summary>
    private static readonly byte[] CanonicalUrlKey = "canonical_url"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>site_root</c>.</summary>
    private static readonly byte[] SiteRootKey = "site_root"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>page_title</c>.</summary>
    private static readonly byte[] PageTitleKey = "page_title"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>body</c>.</summary>
    private static readonly byte[] BodyKey = "body"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>asset_root</c>.</summary>
    private static readonly byte[] AssetRootKey = "asset_root"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>copyright</c>.</summary>
    private static readonly byte[] CopyrightKey = "copyright"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>repo_url</c>.</summary>
    private static readonly byte[] RepoUrlKey = "repo_url"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>edit_url</c>.</summary>
    private static readonly byte[] EditUrlKey = "edit_url"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>scroll_to_top</c>.</summary>
    private static readonly byte[] ScrollToTopKey = "scroll_to_top"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>toc_follow</c>.</summary>
    private static readonly byte[] TocFollowKey = "toc_follow"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>prev_url</c>.</summary>
    private static readonly byte[] PrevUrlKey = "prev_url"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>prev_title</c>.</summary>
    private static readonly byte[] PrevTitleKey = "prev_title"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>next_url</c>.</summary>
    private static readonly byte[] NextUrlKey = "next_url"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>next_title</c>.</summary>
    private static readonly byte[] NextTitleKey = "next_title"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>head_extras</c>.</summary>
    private static readonly byte[] HeadExtrasKey = "head_extras"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>description</c>.</summary>
    private static readonly byte[] DescriptionKey = "description"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>hide_navigation</c>.</summary>
    private static readonly byte[] HideNavigationKey = "hide_navigation"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>hide_toc</c>.</summary>
    private static readonly byte[] HideTocKey = "hide_toc"u8.ToArray();

    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly TOptions _options;

    /// <summary>Output root captured during <see cref="OnConfigureAsync"/>.</summary>
    private DirectoryPath _outputRoot;

    /// <summary>UTF-8 head-extras HTML assembled during <see cref="OnConfigureAsync"/>; empty until it runs.</summary>
    private byte[] _headExtras = [];

    /// <summary>Plugin list captured during <see cref="OnConfigureAsync"/> for static-asset composition at finalize time.</summary>
    private IDocPlugin[] _plugins = [];

    /// <summary>First registered nav-neighbour provider, captured during <see cref="OnConfigureAsync"/>; null when none was registered.</summary>
    private INavNeighboursProvider? _neighbours;

    /// <summary>Per-build edit-link prefix; bytes ending in <c>/</c> ready to concatenate with a relative page path. Empty when edit links are disabled.</summary>
    private byte[] _editUrlPrefix = [];

    /// <summary>Per-build canonical-URL prefix; empty when no site URL is configured.</summary>
    private byte[] _canonicalUrlPrefix = [];

    /// <summary>Whether the build emits pretty <c>foo/index.html</c> URLs.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="ThemePluginBase{TTheme, TOptions}"/> class.</summary>
    /// <param name="options">Theme options.</param>
    /// <param name="theme">Loaded theme.</param>
    protected ThemePluginBase(in TOptions options, TTheme theme)
    {
        _options = options;
        LoadedTheme = theme ?? throw new ArgumentNullException(nameof(theme));
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>Gets the loaded theme.</summary>
    protected TTheme LoadedTheme { get; }

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _outputRoot = context.OutputRoot;
        _headExtras = HeadExtraComposer.Compose(context.Plugins);
        _plugins = context.Plugins;
        _neighbours = FindNavNeighboursProvider(context.Plugins);
        _editUrlPrefix = BuildEditUrlPrefix(_options.RepoUrl, _options.EditUri);
        _canonicalUrlPrefix = BuildCanonicalUrlPrefix(_options.SiteUrl);
        _useDirectoryUrls = context.UseDirectoryUrls;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var html = context.Html;
        var bodyLength = html.WrittenCount;

        var bodyBuffer = ArrayPool<byte>.Shared.Rent(bodyLength);
        try
        {
            html.WrittenSpan.CopyTo(bodyBuffer);
            html.ResetWrittenCount();

            var pageTitle = ResolvePageTitle(context);
            var neighbours = ResolveNeighbours(context.RelativePath);
            var data = new TemplateData(
                new(16, ByteArrayComparer.Instance)
                {
                    [LanguageKey] = _options.Language,
                    [SiteNameKey] = _options.SiteName,
                    [SiteUrlKey] = _options.SiteUrl,
                    [CanonicalUrlKey] = ResolveCanonicalUrlBytes(context.RelativePath),
                    [SiteRootKey] = SiteRootBytes,
                    [PageTitleKey] = Utf8Encoder.Encode(pageTitle),
                    [BodyKey] = new(bodyBuffer, 0, bodyLength),
                    [AssetRootKey] = _options.ResolveAssetRoot(),
                    [CopyrightKey] = _options.Copyright,
                    [RepoUrlKey] = _options.RepoUrl,
                    [EditUrlKey] = ResolveEditUrlBytes(context.RelativePath),
                    [ScrollToTopKey] = _options.EnableScrollToTop ? TruthyBytes : null,
                    [TocFollowKey] = _options.EnableTocFollow ? TruthyBytes : null,
                    [PrevUrlKey] = Utf8Encoder.Encode(NeighbourUrl(neighbours.PreviousPath)),
                    [PrevTitleKey] = neighbours.PreviousTitle,
                    [NextUrlKey] = Utf8Encoder.Encode(NeighbourUrl(neighbours.NextPath)),
                    [NextTitleKey] = neighbours.NextTitle,
                    [HeadExtrasKey] = _headExtras,
                    [DescriptionKey] = ResolveDescription(context),
                    [HideNavigationKey] = NuStreamDocs.Yaml.FrontmatterValueExtractor.ListContains(context.Source.Span, "hide"u8, "navigation"u8) ? TruthyBytes : null,
                    [HideTocKey] = NuStreamDocs.Yaml.FrontmatterValueExtractor.ListContains(context.Source.Span, "hide"u8, "toc"u8) ? TruthyBytes : null,
                },
                sections: null);

            LoadedTheme.Page.Render(data, LoadedTheme.Partials, html);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bodyBuffer);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
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

    /// <summary>Returns the first <see cref="INavNeighboursProvider"/> in <paramref name="plugins"/>, or null when none is registered.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>The provider, or null.</returns>
    private static INavNeighboursProvider? FindNavNeighboursProvider(IDocPlugin[] plugins)
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

    /// <summary>Translates a source-relative neighbour path into the served-page URL.</summary>
    /// <param name="relativePath">Source-relative path; empty when no neighbour exists.</param>
    /// <returns>The served URL, or empty when <paramref name="relativePath"/> is empty.</returns>
    private static string NeighbourUrl(string relativePath)
    {
        if (relativePath is [])
        {
            return string.Empty;
        }

        var path = relativePath.AsSpan();
        var endsWithMarkdown = path.EndsWith(MarkdownExtension, StringComparison.OrdinalIgnoreCase);
        var outputLength = 1 + path.Length + (endsWithMarkdown ? HtmlExtensionGrowth : 0);
        return string.Create(outputLength, (relativePath, endsWithMarkdown), static (dst, state) =>
        {
            dst[0] = '/';
            var src = state.relativePath.AsSpan();
            var stemLength = state.endsWithMarkdown ? src.Length - MarkdownExtension.Length : src.Length;
            for (var i = 0; i < stemLength; i++)
            {
                dst[i + 1] = src[i] is '\\' ? '/' : src[i];
            }

            if (!state.endsWithMarkdown)
            {
                return;
            }

            ".html".AsSpan().CopyTo(dst[(stemLength + 1)..]);
        });
    }

    /// <summary>Resolves the page description — front-matter <c>description:</c>, or empty when absent.</summary>
    /// <param name="context">Per-page render context.</param>
    /// <returns>UTF-8 description bytes, ready for direct emit; empty when no description.</returns>
    private static byte[] ResolveDescription(in PluginRenderContext context)
    {
        var raw = NuStreamDocs.Yaml.FrontmatterValueExtractor.GetScalar(context.Source.Span, "description"u8);
        if (raw.IsEmpty)
        {
            return [];
        }

        return StripYamlQuotes(raw).ToArray();
    }

    /// <summary>Resolves the page title — front-matter <c>title:</c> when present, otherwise the file stem.</summary>
    /// <param name="context">Per-page render context.</param>
    /// <returns>HTML-encoded title text.</returns>
    private static string ResolvePageTitle(in PluginRenderContext context)
    {
        var fromFrontMatter = NuStreamDocs.Yaml.FrontmatterValueExtractor.GetScalar(context.Source.Span, "title"u8);
        if (!fromFrontMatter.IsEmpty)
        {
            var unquoted = StripYamlQuotes(fromFrontMatter);
            return HtmlEncoder.Default.Encode(System.Text.Encoding.UTF8.GetString(unquoted));
        }

        return HtmlEncoder.Default.Encode(Path.GetFileNameWithoutExtension(context.RelativePath));
    }

    /// <summary>Drops a single matching pair of leading/trailing single- or double-quote bytes from <paramref name="value"/>.</summary>
    /// <param name="value">UTF-8 candidate.</param>
    /// <returns>Unquoted slice or <paramref name="value"/> unchanged.</returns>
    private static ReadOnlySpan<byte> StripYamlQuotes(ReadOnlySpan<byte> value)
    {
        if (value.Length >= 2
            && (value[0] is (byte)'"' or (byte)'\'')
            && value[^1] == value[0])
        {
            return value[1..^1];
        }

        return value;
    }

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

    /// <summary>Returns true when <paramref name="value"/> begins with <c>http://</c> or <c>https://</c>.</summary>
    /// <param name="value">UTF-8 candidate URL.</param>
    /// <returns>True for absolute http(s) URLs.</returns>
    private static bool IsAbsoluteUrl(ReadOnlySpan<byte> value) =>
        value.StartsWith("http://"u8) || value.StartsWith("https://"u8);

    /// <summary>Translates forward slashes in <paramref name="relativePath"/> to the active directory separator when needed.</summary>
    /// <param name="relativePath">Relative output path using forward slashes.</param>
    /// <returns>OS-native relative path.</returns>
    private static string TranslateDirectorySeparators(string relativePath)
    {
        if (Path.DirectorySeparatorChar is '/')
        {
            return relativePath;
        }

        return string.Create(relativePath.Length * DirectorySeparatorLength, relativePath, static (dst, state) =>
        {
            for (var i = 0; i < state.Length; i++)
            {
                dst[i] = state[i] is '/' ? Path.DirectorySeparatorChar : state[i];
            }
        });
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

    /// <summary>Returns the URL slug for the page, matching the build pipeline's emit shape.</summary>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="useDirectoryUrls">Whether non-index pages collapse to directory slugs.</param>
    /// <returns>The slug span (no leading <c>/</c>); empty when the page is the root <c>index</c>.</returns>
    private static ReadOnlySpan<char> ToCanonicalSlug(string relativePath, bool useDirectoryUrls)
    {
        var path = relativePath.AsSpan();
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

        if (path.EndsWith(MdExt, StringComparison.OrdinalIgnoreCase))
        {
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

        return path;
    }

    /// <summary>Resolves prev/next neighbours according to the configured footer settings.</summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>The neighbours; <see cref="NavNeighbours.None"/> when the footer is disabled or no provider is registered.</returns>
    private NavNeighbours ResolveNeighbours(string relativePath)
    {
        if (!_options.EnableNavigationFooter || _neighbours is null)
        {
            return NavNeighbours.None;
        }

        return _options.SectionScopedFooter
            ? _neighbours.GetSectionNeighbours(relativePath)
            : _neighbours.GetNeighbours(relativePath);
    }

    /// <summary>Returns the canonical URL for the page; empty when no site URL is configured.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <returns>The canonical URL bytes, or an empty array.</returns>
    private byte[] ResolveCanonicalUrlBytes(string relativePath)
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
    private byte[] ResolveEditUrlBytes(string relativePath)
    {
        if (_editUrlPrefix is [])
        {
            return [];
        }

        var path = relativePath.AsSpan();
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
