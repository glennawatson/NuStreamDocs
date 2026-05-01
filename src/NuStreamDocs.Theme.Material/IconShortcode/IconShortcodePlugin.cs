// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material.IconShortcode;

/// <summary>
/// Material-theme icon shortcode preprocessor. Recognises
/// <c>:material-foo:</c> Material-Icons-font shortcodes and
/// <c>:fontawesome-{style}-foo:</c> Font Awesome shortcodes and
/// rewrites them into the markup the Material theme's bundled
/// stylesheets render.
/// </summary>
/// <remarks>
/// Material classic uses the <c>material-icons</c> font family.
/// Material3 uses <c>material-symbols-outlined</c>; that variant
/// lives in <c>NuStreamDocs.Theme.Material3</c>.
/// </remarks>
public sealed class IconShortcodePlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "material-icon-shortcodes";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        IconShortcodeRewriter.Rewrite(source, writer, "material-icons"u8);
    }
}
