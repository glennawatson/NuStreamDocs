// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3.IconShortcode;

/// <summary>
/// Material3 icon shortcode preprocessor. Recognises
/// <c>:material-foo:</c> Material Symbols shortcodes and
/// <c>:fontawesome-{style}-foo:</c> Font Awesome shortcodes and
/// rewrites them into the markup the Material3 stylesheets render.
/// </summary>
/// <remarks>
/// Material3 uses the variable <c>material-symbols-outlined</c>
/// font; Material classic uses the legacy <c>material-icons</c>
/// family. The two themes ship parallel rewriters so each can
/// evolve its emission shape independently.
/// </remarks>
public sealed class IconShortcodePlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <summary>Optional inline-SVG resolver consulted before the font-ligature fallback.</summary>
    private readonly IIconResolver? _resolver;

    /// <summary>Initializes a new instance of the <see cref="IconShortcodePlugin"/> class with the default font-ligature fallback only.</summary>
    public IconShortcodePlugin()
        : this(null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="IconShortcodePlugin"/> class with an inline-SVG resolver (e.g. <c>NuStreamDocs.Icons.MaterialDesign</c>).</summary>
    /// <param name="resolver">Resolver consulted before the font-ligature fallback; <c>null</c> falls back unconditionally.</param>
    public IconShortcodePlugin(IIconResolver? resolver) => _resolver = resolver;

    /// <inheritdoc/>
    public override string Name => "material3-icon-shortcodes";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        IconShortcodeRewriter.Rewrite(source, writer, "material-symbols-outlined"u8, _resolver);
    }
}
