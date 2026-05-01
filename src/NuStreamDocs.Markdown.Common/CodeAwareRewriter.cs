// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Drives the canonical code-aware rewrite scan loop:
/// fenced-code regions and inline-code spans pass through verbatim,
/// every other byte is offered to a per-cursor probe, and unmatched
/// bytes are copied 1:1.
/// </summary>
/// <remarks>
/// Replaces the hand-rolled copy of this loop that every preprocessor
/// rewriter (caret/tilde, abbr, keys, emoji, arithmatex, critic-markup,
/// inline-hilite, smart-symbols, etc.) used to carry inline.
/// </remarks>
public static class CodeAwareRewriter
{
    /// <summary>Walks <paramref name="source"/> through the canonical scan loop, dispatching markers to <paramref name="tryRewrite"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="tryRewrite">Per-cursor probe.</param>
    public static void Run(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, TryRewriteAt tryRewrite)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(tryRewrite);

        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i) && MarkdownCodeScanner.TryConsumeFence(source, i, out var fenceEnd))
            {
                writer.Write(source[i..fenceEnd]);
                i = fenceEnd;
                continue;
            }

            if (source[i] is (byte)'`')
            {
                var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, i);
                writer.Write(source[i..inlineEnd]);
                i = inlineEnd;
                continue;
            }

            if (tryRewrite(source, i, writer, out var consumed))
            {
                i += consumed;
                continue;
            }

            var dst = writer.GetSpan(1);
            dst[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }
}
