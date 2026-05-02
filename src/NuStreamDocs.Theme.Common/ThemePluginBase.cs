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

    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly TOptions _options;

    /// <summary>Output root captured during <see cref="OnConfigureAsync"/>.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>UTF-8 head-extras HTML assembled during <see cref="OnConfigureAsync"/>; empty until it runs.</summary>
    private byte[] _headExtras = [];

    /// <summary>Plugin list captured during <see cref="OnConfigureAsync"/> for static-asset composition at finalize time.</summary>
    private IDocPlugin[] _plugins = [];

    /// <summary>First registered nav-neighbour provider, captured during <see cref="OnConfigureAsync"/>; null when none was registered.</summary>
    private INavNeighboursProvider? _neighbours;

    /// <summary>Per-build constant scalars cached during <see cref="OnConfigureAsync"/> so per-page renders avoid re-encoding them.</summary>
    private byte[] _languageBytes = [];

    /// <summary>UTF-8 site name; cached during <see cref="OnConfigureAsync"/>.</summary>
    private byte[] _siteNameBytes = [];

    /// <summary>UTF-8 asset root; cached during <see cref="OnConfigureAsync"/>.</summary>
    private byte[] _assetRootBytes = [];

    /// <summary>UTF-8 copyright string; cached during <see cref="OnConfigureAsync"/>.</summary>
    private byte[] _copyrightBytes = [];

    /// <summary>UTF-8 repo URL; cached during <see cref="OnConfigureAsync"/>.</summary>
    private byte[] _repoUrlBytes = [];

    /// <summary>Repo/edit prefix normalized once per build for per-page edit URL composition.</summary>
    private string _editUrlPrefix = string.Empty;

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
        _languageBytes = Utf8Encoder.Encode(_options.Language);
        _siteNameBytes = Utf8Encoder.Encode(_options.SiteName);
        _assetRootBytes = Utf8Encoder.Encode(_options.ResolveAssetRoot());
        _copyrightBytes = Utf8Encoder.Encode(_options.Copyright);
        _repoUrlBytes = Utf8Encoder.Encode(_options.RepoUrl);
        _editUrlPrefix = BuildEditUrlPrefix(_options.RepoUrl, _options.EditUri);

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

            var pageTitle = HtmlEncoder.Default.Encode(Path.GetFileNameWithoutExtension(context.RelativePath));
            var editUrl = ResolveEditUrl(context.RelativePath);
            var neighbours = ResolveNeighbours(context.RelativePath);
            var data = new TemplateData(
                new(14, ByteArrayComparer.Instance)
                {
                    [LanguageKey] = _languageBytes,
                    [SiteNameKey] = _siteNameBytes,
                    [SiteRootKey] = SiteRootBytes,
                    [PageTitleKey] = Utf8Encoder.Encode(pageTitle),
                    [BodyKey] = new(bodyBuffer, 0, bodyLength),
                    [AssetRootKey] = _assetRootBytes,
                    [CopyrightKey] = _copyrightBytes,
                    [RepoUrlKey] = _repoUrlBytes,
                    [EditUrlKey] = Utf8Encoder.Encode(editUrl),
                    [ScrollToTopKey] = _options.EnableScrollToTop ? TruthyBytes : null,
                    [TocFollowKey] = _options.EnableTocFollow ? TruthyBytes : null,
                    [PrevUrlKey] = Utf8Encoder.Encode(NeighbourUrl(neighbours.PreviousPath)),
                    [PrevTitleKey] = Utf8Encoder.Encode(neighbours.PreviousTitle),
                    [NextUrlKey] = Utf8Encoder.Encode(NeighbourUrl(neighbours.NextPath)),
                    [NextTitleKey] = Utf8Encoder.Encode(neighbours.NextTitle),
                    [HeadExtrasKey] = _headExtras,
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
        var root = _outputRoot is [] ? context.OutputRoot : _outputRoot;
        if (root is [])
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

    /// <summary>Normalizes the repo/edit roots once per build for per-page edit URL composition.</summary>
    /// <param name="repoUrl">Configured repository URL.</param>
    /// <param name="editUri">Configured edit path inside the repository.</param>
    /// <returns>The normalized prefix ending in <c>/</c>, or empty when edit links are disabled.</returns>
    private static string BuildEditUrlPrefix(string repoUrl, string editUri)
    {
        if (repoUrl is [] || editUri is [])
        {
            return string.Empty;
        }

        var repo = repoUrl.AsSpan().TrimEnd('/');
        var edit = editUri.AsSpan().Trim('/');
        return string.Create(repo.Length + edit.Length + EditUrlSeparatorCount, (repoUrl, editUri, repoLength: repo.Length, editLength: edit.Length), static (dst, state) =>
        {
            state.repoUrl.AsSpan(0, state.repoLength).CopyTo(dst);
            dst[state.repoLength] = '/';
            state.editUri.AsSpan().Trim('/').CopyTo(dst[(state.repoLength + 1)..]);
            dst[^1] = '/';
        });
    }

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

    /// <summary>Builds the per-page edit URL from the configured repo URL + edit prefix + the page's relative path.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <returns>The edit URL, or an empty string when not configured.</returns>
    private string ResolveEditUrl(string relativePath)
    {
        if (_editUrlPrefix is [])
        {
            return string.Empty;
        }

        var path = relativePath.AsSpan();
        var start = path is ['/', ..] ? 1 : 0;
        return string.Create(_editUrlPrefix.Length + path.Length - start, (_editUrlPrefix, relativePath, start), static (dst, state) =>
        {
            state._editUrlPrefix.AsSpan().CopyTo(dst);
            var write = state._editUrlPrefix.Length;
            var src = state.relativePath.AsSpan();
            for (var i = state.start; i < src.Length; i++)
            {
                dst[write++] = src[i] is '\\' ? '/' : src[i];
            }
        });
    }
}
