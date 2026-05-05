// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Shared base for theme-specific icon shortcode preprocessors.
/// </summary>
public abstract class ThemeIconShortcodePluginBase : IPagePreRenderPlugin
{
    /// <summary>Optional inline-SVG resolver consulted before the font-ligature fallback.</summary>
    private readonly IIconResolver? _resolver;

    /// <summary>Initializes a new instance of the <see cref="ThemeIconShortcodePluginBase"/> class.</summary>
    /// <param name="resolver">Resolver consulted before the font-ligature fallback; <c>null</c> falls back unconditionally.</param>
    protected ThemeIconShortcodePluginBase(IIconResolver? resolver) => _resolver = resolver;

    /// <inheritdoc/>
    public abstract ReadOnlySpan<byte> Name { get; }

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <summary>Gets the theme-specific font class emitted for Material shortcodes.</summary>
    protected abstract ReadOnlySpan<byte> IconFontClass { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Icon shortcodes are <c>:material-name:</c> / <c>:fontawesome-name:</c> — every match needs
    /// at least one <c>:</c> byte, so pages without one skip both the rewriter scan and the
    /// pipeline scratch rental.
    /// </remarks>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => source.IndexOf((byte)':') >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        IconShortcodeRewriter.Rewrite(context.Source, context.Output, IconFontClass, _resolver);
}
