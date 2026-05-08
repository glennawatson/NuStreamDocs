// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Layouts;

/// <summary>
/// Wraps the rendered HTML of pages whose frontmatter declares a
/// <c>template:</c> in the named layout. The layout is processed with a
/// minimal Jinja2 subset — <c>{{ page.X }}</c> variables, <c>{% include %}</c>,
/// and <c>{% extends %}</c> + <c>{% block %}</c> inheritance with
/// <c>{{ super() }}</c>.
/// </summary>
public sealed class LayoutsPlugin : IPagePostRenderPlugin, IBuildConfigurePlugin
{
    /// <summary>Tiebreak inside <see cref="PluginBand.Latest"/> chosen so the layout swap runs immediately before the theme shell wraps the body in full-page chrome.</summary>
    private const int PostRenderTiebreak = -1;

    /// <summary>Configured options.</summary>
    private readonly LayoutsOptions _options;

    /// <summary>Logger used for diagnostic warnings.</summary>
    private readonly ILogger _logger;

    /// <summary>Per-build parse cache so each unique template file parses exactly once even when hundreds of pages share it.</summary>
    private readonly TemplateCache _cache = new();

    /// <summary>Initializes a new instance of the <see cref="LayoutsPlugin"/> class with default options.</summary>
    public LayoutsPlugin()
        : this(LayoutsOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LayoutsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public LayoutsPlugin(LayoutsOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LayoutsPlugin"/> class with a logger.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger.</param>
    public LayoutsPlugin(LayoutsOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "layouts"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <summary>Empties the per-build template cache so every build (including each rebuild in serve mode) parses templates from disk afresh.</summary>
    /// <param name="context">Build configuration context (unused — the cache is plugin-scoped).</param>
    /// <param name="cancellationToken">Cancellation token (unused — the operation is synchronous).</param>
    /// <returns>Completed task.</returns>
    /// <remarks>
    /// The cache is cleared at the start of each build so serve-mode
    /// rebuilds always see fresh template bytes from disk. Cache lifetime
    /// is therefore one build; the dictionary never accumulates stale
    /// entries across builds.
    /// </remarks>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _cache.Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <c>true</c> unconditionally — the only signal that decides whether to apply a layout
    /// lives in the page's frontmatter, which the post-render hook reads from
    /// <see cref="PagePostRenderContext.Source"/>. The <see cref="PostRender"/> body short-circuits
    /// to a passthrough copy when no <c>template:</c> key is present.
    /// </remarks>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => true;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var template = FrontmatterReader.GetScalar(context.Source, "template"u8);
        if (template.IsEmpty || _options.TemplateDirectory.IsEmpty)
        {
            CopyThrough(context.Html, context.Output);
            return;
        }

        var url = ToUrlBytes(context.RelativePath);
        var layoutContext = LayoutContext.FromPage(context.Source, context.Html, url);
        if (LayoutRenderer.Render(template, _options.TemplateDirectory, layoutContext, _options.MaxIncludeDepth, context.Output, _logger, _cache))
        {
            return;
        }

        CopyThrough(context.Html, context.Output);
    }

    /// <summary>Returns the per-build template cache (test/diagnostic accessor; internal-only).</summary>
    /// <returns>The plugin's parse cache.</returns>
    internal TemplateCache GetCacheForTests() => _cache;

    /// <summary>Copies <paramref name="source"/> through to <paramref name="writer"/> without scanning.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void CopyThrough(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        if (source.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(source.Length);
        source.CopyTo(dst);
        writer.Advance(source.Length);
    }

    /// <summary>Builds the <c>page.url</c> bytes from the page's relative path — forward-slashes, no leading slash, <c>.md</c> stripped to <c>.html</c>-equivalent.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <returns>UTF-8 url bytes.</returns>
    private static byte[] ToUrlBytes(FilePath relativePath)
    {
        if (relativePath.IsEmpty)
        {
            return [];
        }

        var raw = relativePath.Value.Replace('\\', '/').TrimStart('/');
        return Encoding.UTF8.GetBytes(raw);
    }
}
