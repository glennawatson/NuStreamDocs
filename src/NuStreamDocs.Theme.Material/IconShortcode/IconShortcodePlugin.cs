// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material.IconShortcode;

/// <summary>
/// Material-theme icon shortcode preprocessor. Recognizes
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
public sealed class IconShortcodePlugin : ThemeIconShortcodePluginBase
{
    /// <summary>Initializes a new instance of the <see cref="IconShortcodePlugin"/> class with the default font-ligature fallback only.</summary>
    public IconShortcodePlugin()
        : this(null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="IconShortcodePlugin"/> class with an inline-SVG resolver (e.g. <c>NuStreamDocs.Icons.MaterialDesign</c>).</summary>
    /// <param name="resolver">Resolver consulted before the font-ligature fallback; <c>null</c> falls back unconditionally.</param>
    public IconShortcodePlugin(IIconResolver? resolver)
        : base(resolver)
    {
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Name => "material-icon-shortcodes"u8;

    /// <inheritdoc/>
    protected override ReadOnlySpan<byte> IconFontClass => "material-icons"u8;
}
