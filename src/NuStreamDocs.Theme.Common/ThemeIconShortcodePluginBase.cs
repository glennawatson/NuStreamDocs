// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Shared base for theme-specific icon shortcode preprocessors.
/// </summary>
public abstract class ThemeIconShortcodePluginBase : DocPluginBase, IMarkdownPreprocessor
{
    /// <summary>Optional inline-SVG resolver consulted before the font-ligature fallback.</summary>
    private readonly IIconResolver? _resolver;

    /// <summary>Initializes a new instance of the <see cref="ThemeIconShortcodePluginBase"/> class.</summary>
    /// <param name="resolver">Resolver consulted before the font-ligature fallback; <c>null</c> falls back unconditionally.</param>
    protected ThemeIconShortcodePluginBase(IIconResolver? resolver) => _resolver = resolver;

    /// <summary>Gets the theme-specific font class emitted for Material shortcodes.</summary>
    protected abstract ReadOnlySpan<byte> IconFontClass { get; }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        IconShortcodeRewriter.Rewrite(source, writer, IconFontClass, _resolver);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath) =>
        Preprocess(source, writer);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;
}
