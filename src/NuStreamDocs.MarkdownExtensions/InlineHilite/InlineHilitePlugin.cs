// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.InlineHilite;

/// <summary>
/// Inline-highlight plugin (pymdownx.inlinehilite). Rewrites
/// inline code that begins with the <c>#!lang</c> shebang into a
/// <c>&lt;code class="highlight language-lang"&gt;…&lt;/code&gt;</c>
/// element so a syntax-highlighting layer can pick it up.
/// </summary>
/// <remarks>
/// The block-level highlight pipeline lives in
/// <c>NuStreamDocs.Highlight</c>; this plugin just emits the
/// language-class envelope for inline code so the block and inline
/// styles share their classes.
/// </remarks>
public sealed class InlineHilitePlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override byte[] Name => "inlinehilite"u8.ToArray();

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasInlineHiliteFence(source);

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        InlineHiliteRewriter.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath) =>
        Preprocess(source, writer);
}
