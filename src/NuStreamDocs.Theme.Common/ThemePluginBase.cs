// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Reflection;
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

    /// <summary>UTF-8 template-data key for <c>repo_label</c> — the short host-less repo name shown next to the source icon.</summary>
    private static readonly byte[] RepoLabelKey = "repo_label"u8.ToArray();

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

    /// <summary>UTF-8 template-data key for <c>author</c>.</summary>
    private static readonly byte[] AuthorKey = "author"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>hide_navigation</c>.</summary>
    private static readonly byte[] HideNavigationKey = "hide_navigation"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>hide_toc</c>.</summary>
    private static readonly byte[] HideTocKey = "hide_toc"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>favicon</c>.</summary>
    private static readonly byte[] FaviconKey = "favicon"u8.ToArray();

    /// <summary>UTF-8 template-data key for <c>generator</c>.</summary>
    private static readonly byte[] GeneratorKey = "generator"u8.ToArray();

    /// <summary>UTF-8 generator value emitted as <c>nustreamdocs-{version}</c>; encoded once at type init.</summary>
    private static readonly byte[] GeneratorBytes = BuildGeneratorBytes();

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

    /// <summary>Short owner/repo label derived from the configured repo URL; empty when no repo URL is configured.</summary>
    private byte[] _repoLabel = [];

    /// <summary>Per-build canonical-URL prefix; empty when no site URL is configured.</summary>
    private byte[] _canonicalUrlPrefix = [];

    /// <summary>Site-wide author captured from the configure context; used as the per-page <c>&lt;meta author&gt;</c> fallback when the page has no front-matter <c>author:</c>.</summary>
    private byte[] _siteAuthor = [];

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
        _repoLabel = BuildRepoLabel(_options.RepoUrl);
        _siteAuthor = context.SiteAuthor;
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
                new(22, ByteArrayComparer.Instance)
                {
                    [LanguageKey] = _options.Language,
                    [SiteNameKey] = _options.SiteName,
                    [SiteUrlKey] = _options.SiteUrl,
                    [CanonicalUrlKey] = ResolveCanonicalUrlBytes(context.RelativePath),
                    [SiteRootKey] = SiteRootBytes,
                    [PageTitleKey] = Utf8Encoder.Encode(pageTitle),
                    [BodyKey] = new(bodyBuffer, 0, bodyLength),
                    [AssetRootKey] = ResolvePageRelativeAssetRoot(_options.ResolveAssetRoot(), context.RelativePath),
                    [CopyrightKey] = _options.Copyright,
                    [RepoUrlKey] = _options.RepoUrl,
                    [RepoLabelKey] = _repoLabel,
                    [EditUrlKey] = ResolveEditUrlBytes(context.RelativePath),
                    [ScrollToTopKey] = _options.EnableScrollToTop ? TruthyBytes : null,
                    [TocFollowKey] = _options.EnableTocFollow ? TruthyBytes : null,
                    [PrevUrlKey] = Utf8Encoder.Encode(NeighbourUrl(neighbours.PreviousPath)),
                    [PrevTitleKey] = neighbours.PreviousTitle,
                    [NextUrlKey] = Utf8Encoder.Encode(NeighbourUrl(neighbours.NextPath)),
                    [NextTitleKey] = neighbours.NextTitle,
                    [HeadExtrasKey] = RewriteHeadExtraAssetHrefs(_headExtras, context.RelativePath),
                    [DescriptionKey] = ResolveDescription(context),
                    [HideNavigationKey] = NuStreamDocs.Yaml.FrontmatterValueExtractor.ListContains(context.Source.Span, "hide"u8, "navigation"u8) ? TruthyBytes : null,
                    [HideTocKey] = NuStreamDocs.Yaml.FrontmatterValueExtractor.ListContains(context.Source.Span, "hide"u8, "toc"u8) ? TruthyBytes : null,
                    [GeneratorKey] = GeneratorBytes,
                    [FaviconKey] = _options.Favicon,
                    [AuthorKey] = ResolveAuthor(context, _siteAuthor),
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

    /// <summary>Resolves the page author — front-matter <c>author:</c>, falling back to the site-wide value from the configure context.</summary>
    /// <param name="context">Per-page render context.</param>
    /// <param name="siteAuthor">Site-wide author bytes captured at configure time; empty when not configured.</param>
    /// <returns>UTF-8 author bytes; empty when neither front-matter nor site_author is set.</returns>
    private static byte[] ResolveAuthor(in PluginRenderContext context, byte[] siteAuthor)
    {
        var raw = NuStreamDocs.Yaml.FrontmatterValueExtractor.GetScalar(context.Source.Span, "author"u8);
        if (!raw.IsEmpty)
        {
            return StripYamlQuotes(raw).ToArray();
        }

        return siteAuthor;
    }

    /// <summary>Resolves the page title — front-matter <c>title:</c>, then the first markdown H1, then the file stem.</summary>
    /// <param name="context">Per-page render context.</param>
    /// <returns>HTML-encoded title text.</returns>
    private static string ResolvePageTitle(in PluginRenderContext context)
    {
        var source = context.Source.Span;
        var fromFrontMatter = NuStreamDocs.Yaml.FrontmatterValueExtractor.GetScalar(source, "title"u8);
        if (!fromFrontMatter.IsEmpty)
        {
            var unquoted = StripYamlQuotes(fromFrontMatter);
            return HtmlEncoder.Default.Encode(System.Text.Encoding.UTF8.GetString(unquoted));
        }

        var firstHeading = NuStreamDocs.Markdown.MarkdownH1Scanner.FindFirst(source);
        if (!firstHeading.IsEmpty)
        {
            return HtmlEncoder.Default.Encode(System.Text.Encoding.UTF8.GetString(firstHeading));
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

    /// <summary>Builds the <c>nustreamdocs-{version}</c> generator value; the version is read from the assembly informational version with any <c>+sha</c> build-metadata suffix stripped.</summary>
    /// <returns>UTF-8 bytes of the generator string.</returns>
    private static byte[] BuildGeneratorBytes()
    {
        const string Prefix = "nustreamdocs-";
        var informational = typeof(ThemePluginBase<TTheme, TOptions>).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var span = informational.AsSpan();
        var plus = span.IndexOf('+');
        if (plus >= 0)
        {
            span = span[..plus];
        }

        var totalChars = Prefix.Length + span.Length;
        var dst = new byte[totalChars];
        for (var i = 0; i < Prefix.Length; i++)
        {
            dst[i] = (byte)Prefix[i];
        }

        for (var i = 0; i < span.Length; i++)
        {
            // Semantic-version characters are ASCII; safe to narrow.
            dst[Prefix.Length + i] = (byte)span[i];
        }

        return dst;
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

    /// <summary>Counts directory-segment depth of <paramref name="relativePath"/> for page-relative URL composition.</summary>
    /// <param name="relativePath">Source-relative page path (e.g. <c>guide/intro.md</c>).</param>
    /// <returns>Number of <c>/</c> separators between the input root and the page's directory.</returns>
    private static int PageDepth(string relativePath)
    {
        var path = relativePath.AsSpan();
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

        return depth;
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
    /// <returns>Page-relative asset-root bytes, or the original bytes when the input is an absolute URL.</returns>
    private static byte[] ResolvePageRelativeAssetRoot(byte[] assetRoot, string relativePath)
    {
        if (assetRoot is [] || IsAbsoluteUrl(assetRoot))
        {
            return assetRoot;
        }

        // Strip a leading '/' so concatenation with the page-relative prefix doesn't produce a site-root absolute URL.
        var span = assetRoot.AsSpan();
        var trimmed = span is [(byte)'/', ..] ? span[1..] : span;
        var prefix = PageRelativePrefixBytes(PageDepth(relativePath));
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
    /// <returns>Rewritten bytes; the original blob when no occurrences match or the page is at depth 0 and only the leading slash needs trimming.</returns>
    private static byte[] RewriteHeadExtraAssetHrefs(byte[] headExtras, string relativePath)
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

        var prefix = PageRelativePrefixBytes(PageDepth(relativePath));
        var writer = new ArrayBufferWriter<byte>(headExtras.Length);
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
