// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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
/// <see cref="OnConfigureAsync"/> sets <c>use_directory_urls: true</c>,
/// hrefs become <c>foo/</c> instead of <c>foo.html</c> and
/// <c>index.md</c> targets resolve to the parent directory's root.
/// </remarks>
public sealed class MarkdownLinkRewriterPlugin(bool? useDirectoryUrls) : IDocPlugin
{
    /// <summary>Caller-supplied directory-URL override; null defers to the config.</summary>
    private readonly bool? _useDirectoryUrlsOverride = useDirectoryUrls;

    /// <summary>Resolved directory-URL toggle captured during <see cref="OnConfigureAsync"/>.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="MarkdownLinkRewriterPlugin"/> class with no caller override (config-driven).</summary>
    public MarkdownLinkRewriterPlugin()
        : this(null)
    {
    }

    /// <inheritdoc/>
    public string Name => "markdown-link-rewriter";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _useDirectoryUrls = _useDirectoryUrlsOverride ?? context.UseDirectoryUrls;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var html = context.Html;
        var rendered = html.WrittenSpan;
        if (!MarkdownLinkRewriter.NeedsRewrite(rendered))
        {
            return ValueTask.CompletedTask;
        }

        // Rewrite into a pooled scratch writer, then swap the bytes back into the page writer.
        // Two-buffer dance is necessary because we read from html.WrittenSpan while writing the
        // rewritten output — same writer can't be both source and sink.
        using var scratch = PageBuilderPool.Rent(rendered.Length);
        MarkdownLinkRewriter.RewriteInto(rendered, _useDirectoryUrls, scratch.Writer);
        html.ResetWrittenCount();
        html.Write(scratch.Writer.WrittenSpan);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }
}
