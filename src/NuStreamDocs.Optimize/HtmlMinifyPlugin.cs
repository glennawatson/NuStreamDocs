// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Optimize;

/// <summary>
/// Plugin that rewrites the per-page HTML buffer in place with whitespace
/// collapsed and HTML comments stripped. Pre/code/textarea/script/style
/// blocks are passed through verbatim.
/// </summary>
public sealed class HtmlMinifyPlugin(HtmlMinifyOptions options) : IPagePostResolvePlugin
{
    /// <summary>Configured options.</summary>
    private readonly HtmlMinifyOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>Initializes a new instance of the <see cref="HtmlMinifyPlugin"/> class with default options.</summary>
    public HtmlMinifyPlugin()
        : this(HtmlMinifyOptions.Default)
    {
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "html-minify"u8;

    /// <inheritdoc/>
    public PluginPriority PostResolvePriority => new(PluginBand.Latest);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => true;

    /// <inheritdoc/>
    public void Rewrite(in PagePostResolveContext context) =>
        HtmlMinifier.Minify(context.Html, context.Output, _options);
}
