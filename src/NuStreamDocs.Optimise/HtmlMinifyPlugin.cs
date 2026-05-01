// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Optimise;

/// <summary>
/// Plugin that rewrites the per-page HTML buffer in place with whitespace
/// collapsed and HTML comments stripped. Pre/code/textarea/script/style
/// blocks are passed through verbatim.
/// </summary>
public sealed class HtmlMinifyPlugin(HtmlMinifyOptions options) : IDocPlugin
{
    /// <summary>Configured options.</summary>
    private readonly HtmlMinifyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Initializes a new instance of the <see cref="HtmlMinifyPlugin"/> class with default options.</summary>
    public HtmlMinifyPlugin()
        : this(HtmlMinifyOptions.Default)
    {
    }

    /// <inheritdoc/>
    public string Name => "html-minify";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        HtmlSnapshotRewriter.Rewrite(
            context.Html,
            _options,
            static (snapshot, writer, options) => HtmlMinifier.Minify(snapshot, writer, options));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinaliseAsync(PluginFinaliseContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }
}
