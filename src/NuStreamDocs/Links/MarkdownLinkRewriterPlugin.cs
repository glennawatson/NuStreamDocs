// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Links;

/// <summary>
/// Post-render plugin that rewrites <c>&lt;a href="…/foo.md"&gt;</c>
/// hrefs into the rendered <c>.html</c> filename so cross-page
/// links resolve in the static-site output. External URLs and
/// non-Markdown hrefs are passed through unchanged.
/// </summary>
/// <remarks>
/// Honors the directory-URL toggle: when constructed with
/// <c>useDirectoryUrls = true</c>, or when the config seen during
/// <see cref="ConfigureAsync"/> sets <c>use_directory_urls: true</c>,
/// hrefs become <c>foo/</c> instead of <c>foo.html</c> and
/// <c>index.md</c> targets resolve to the parent directory's root.
/// </remarks>
public sealed class MarkdownLinkRewriterPlugin(bool? useDirectoryUrls) : IBuildConfigurePlugin, IPagePostRenderPlugin
{
    /// <summary>Caller-supplied directory-URL override; null defers to the config.</summary>
    private readonly bool? _useDirectoryUrlsOverride = useDirectoryUrls;

    /// <summary>Resolved directory-URL toggle captured during <see cref="ConfigureAsync"/>.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="MarkdownLinkRewriterPlugin"/> class with no caller override (config-driven).</summary>
    public MarkdownLinkRewriterPlugin()
        : this(null)
    {
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "markdown-link-rewriter"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _useDirectoryUrls = _useDirectoryUrlsOverride ?? context.UseDirectoryUrls;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => MarkdownLinkRewriter.NeedsRewrite(html);

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var prependParent = _useDirectoryUrls && IsNonIndexMarkdownPath(context.RelativePath);
        MarkdownLinkRewriter.RewriteInto(context.Html, _useDirectoryUrls, prependParent, context.Output);
    }

    /// <summary>Returns true when <paramref name="relativePath"/> names a non-index markdown file (gains an extra directory level under directory-URL mode).</summary>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <returns>True for non-index markdown pages.</returns>
    private static bool IsNonIndexMarkdownPath(FilePath relativePath)
    {
        if (relativePath.IsEmpty)
        {
            return false;
        }

        var name = Path.GetFileNameWithoutExtension(relativePath.Value.AsSpan());
        return !name.Equals("index", StringComparison.OrdinalIgnoreCase);
    }
}
