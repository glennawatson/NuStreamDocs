// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.MdInHtml;

/// <summary>
/// Markdown-in-HTML plugin (Markdown Extra <c>md_in_html</c>).
/// Recognizes HTML block-level tags carrying
/// <c>markdown="1"</c> / <c>markdown="block"</c>, strips the
/// attribute, and pads the inner content with blank lines so the
/// CommonMark block parser ends the surrounding HTML block early
/// and parses the body as Markdown.
/// </summary>
/// <remarks>
/// This is the block-mode subset of pymdownx + Markdown Extra's
/// <c>md_in_html</c>; <c>markdown="span"</c> (inline-only) is
/// treated the same as block mode for now.
/// </remarks>
public sealed class MdInHtmlPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "md_in_html";

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasMdInHtmlAttribute(source);

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        MdInHtmlRewriter.Rewrite(source, writer);
    }
}
