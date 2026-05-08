// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Common;
using NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

namespace NuStreamDocs.MarkdownExtensions.AttrList;

/// <summary>Stateless HTML post-pass that lifts <c>{: ... }</c> tokens out of the rendered HTML and into attributes on the matching opening tag.</summary>
/// <remarks>
/// Three patterns: paired inline (<c>&lt;a href="…"&gt;text&lt;/a&gt;{: .x }</c>),
/// void inline (<c>&lt;img src="…"&gt;{: .x }</c>), and block
/// (<c>&lt;hN&gt;Heading {: .x }&lt;/hN&gt;</c>). The stages run in
/// that order so a <c>{: }</c> sitting inside a block element is
/// claimed by the inline matcher first; running block first would let
/// it swallow inline tokens that belong to nested elements. Stages
/// share two two-buffer <see cref="ArrayBufferWriter{T}"/>s — only two
/// buffer allocations regardless of how many stages rewrite, and zero
/// per-stage <c>ToArray</c> copies.
/// </remarks>
internal static class AttrListRewriter
{
    /// <summary>Delegate matching the byte-level scanner signature for one of the three passes.</summary>
    /// <param name="source">UTF-8 source span.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one rewrite happened.</returns>
    private delegate bool ByteStage(ReadOnlySpan<byte> source, IBufferWriter<byte> sink);

    /// <summary>Returns true when <paramref name="html"/> may contain an attr-list marker.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <returns>True when one of the recognized opener byte-sequences appears anywhere in the
    /// document; the per-position matchers in <see cref="AttrListMarker.TryMatchMarker"/> apply
    /// the full position + shape check.</returns>
    /// <remarks>
    /// Two vectorized <see cref="System.MemoryExtensions.IndexOf{T}(System.ReadOnlySpan{T}, System.ReadOnlySpan{T})"/>
    /// probes — canonical Python-Markdown <c>{:</c> anywhere, or the mkdocs-material shorthand
    /// <c>{ </c> (open-brace + whitespace) which catches both the trailing-after-tag form
    /// (<c>&lt;a&gt;text&lt;/a&gt;{ .class }</c>) and the in-body form
    /// (<c>&lt;h1&gt;Heading { #id }&lt;/h1&gt;</c>). Pages whose only braces live in code
    /// blocks <i>without</i> a following whitespace (e.g. <c>{foo:1}</c> JSON) skip all three
    /// byte-stage scanners; the per-position matchers reject false positives where the gate
    /// over-matches.
    /// </remarks>
    public static bool NeedsRewrite(ReadOnlySpan<byte> html) =>
        html.IndexOf("{:"u8) >= 0
        || html.IndexOf("{ "u8) >= 0
        || html.IndexOf("{#"u8) >= 0
        || html.IndexOf("{."u8) >= 0
        || ContainsBraceBareKeyShorthand(html);

    /// <summary>Rewrites every block- and inline-level attr-list token in <paramref name="html"/> directly into <paramref name="sink"/>.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <param name="sink">UTF-8 sink the rewritten HTML is encoded into.</param>
    public static void RewriteInto(ReadOnlySpan<byte> html, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (html.IsEmpty)
        {
            return;
        }

        using var rentalA = PageBuilderPool.Rent(html.Length);
        using var rentalB = PageBuilderPool.Rent(html.Length);
        using var rentalC = PageBuilderPool.Rent(html.Length);
        StageBuffers slots = new(rentalA.Writer, rentalB.Writer);

        // Pre-pass: the markdown inline renderer escapes `"` to `&quot;` inside paragraph text,
        // so the value parsers downstream never see literal quotes inside `{key="val"}` markers.
        // Decode HTML quote entities only inside brace regions before the rewrite stages run.
        var prepared = DecodeQuoteEntitiesInBraces(html, rentalC.Writer);

        // Stage 1 reads the prepared bytes directly; subsequent stages
        // read from whichever two-buffer buffer holds the latest output.
        var current = RunFirstStage(prepared, ref slots, InlinePairedAttrListBytes.RewriteInto);
        current = RunStage(current, ref slots, InlineVoidAttrListBytes.RewriteInto);
        current = RunStage(current, ref slots, BlockAttrListBytes.RewriteInto);

        sink.Write(current.Span);
    }

    /// <summary>True when <paramref name="html"/> contains a <c>{</c> immediately followed by an ASCII letter or underscore — the bare-key shorthand <c>{width=…}</c>.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <returns>True on at least one match.</returns>
    private static bool ContainsBraceBareKeyShorthand(ReadOnlySpan<byte> html)
    {
        var i = html.IndexOf((byte)'{');
        while (i >= 0 && i + 1 < html.Length)
        {
            var b = html[i + 1];
            if (b is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (byte)'_')
            {
                return true;
            }

            var rest = html[(i + 1)..].IndexOf((byte)'{');
            if (rest < 0)
            {
                return false;
            }

            i += rest + 1;
        }

        return false;
    }

    /// <summary>Replaces HTML quote entities inside <c>{...}</c> regions with literal <c>"</c>; passes spans outside braces through unchanged.</summary>
    /// <param name="html">Source HTML.</param>
    /// <param name="scratch">Scratch sink used when at least one entity is rewritten.</param>
    /// <returns><paramref name="html"/> when no quote entity sits inside a brace region; otherwise <paramref name="scratch"/>'s written span.</returns>
    private static ReadOnlySpan<byte> DecodeQuoteEntitiesInBraces(ReadOnlySpan<byte> html, ArrayBufferWriter<byte> scratch)
    {
        if (FindEntityInsideBrace(html) < 0)
        {
            return html;
        }

        scratch.ResetWrittenCount();
        var i = 0;
        var insideBrace = false;
        while (i < html.Length)
        {
            var b = html[i];
            if (b is (byte)'{')
            {
                insideBrace = true;
            }
            else if (b is (byte)'}')
            {
                insideBrace = false;
            }
            else if (insideBrace && b is (byte)'&' && TryConsumeQuoteEntity(html, ref i, scratch))
            {
                continue;
            }

            AppendByte(scratch, b);
            i++;
        }

        return scratch.WrittenSpan;
    }

    /// <summary>If a quote entity starts at <paramref name="i"/>, writes <c>"</c> to <paramref name="scratch"/> and advances past the entity.</summary>
    /// <param name="html">Source HTML.</param>
    /// <param name="i">Cursor — advanced past the entity on a hit.</param>
    /// <param name="scratch">Sink for the decoded byte.</param>
    /// <returns>True when an entity was consumed.</returns>
    private static bool TryConsumeQuoteEntity(ReadOnlySpan<byte> html, ref int i, ArrayBufferWriter<byte> scratch)
    {
        var rest = html[i..];
        var consumed = MatchQuoteEntityLength(rest);
        if (consumed is 0)
        {
            return false;
        }

        AppendByte(scratch, (byte)'"');
        i += consumed;
        return true;
    }

    /// <summary>Returns the byte-length of the quote entity at the start of <paramref name="rest"/>, or <c>0</c> when none matches.</summary>
    /// <param name="rest">Bytes starting at the candidate <c>&amp;</c>.</param>
    /// <returns>Match length (5 or 6) or 0.</returns>
    private static int MatchQuoteEntityLength(ReadOnlySpan<byte> rest)
    {
        if (rest.StartsWith("&quot;"u8))
        {
            return "&quot;"u8.Length;
        }

        if (rest.StartsWith("&#34;"u8))
        {
            return "&#34;"u8.Length;
        }

        if (rest.StartsWith("&#x22;"u8) || rest.StartsWith("&#X22;"u8))
        {
            return "&#x22;"u8.Length;
        }

        return 0;
    }

    /// <summary>Finds the first <c>&amp;</c> sitting between an opening <c>{</c> and its matching <c>}</c>; returns <c>-1</c> when none.</summary>
    /// <param name="html">Source HTML.</param>
    /// <returns>Offset of the first qualifying <c>&amp;</c>, or <c>-1</c>.</returns>
    private static int FindEntityInsideBrace(ReadOnlySpan<byte> html)
    {
        var depth = 0;
        for (var i = 0; i < html.Length; i++)
        {
            var b = html[i];
            if (b is (byte)'{')
            {
                depth++;
                continue;
            }

            if (b is (byte)'}')
            {
                if (depth > 0)
                {
                    depth--;
                }

                continue;
            }

            if (depth > 0 && b is (byte)'&')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Appends a single byte to <paramref name="sink"/>.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="b">Byte to append.</param>
    private static void AppendByte(ArrayBufferWriter<byte> sink, byte b)
    {
        var span = sink.GetSpan(1);
        span[0] = b;
        sink.Advance(1);
    }

    /// <summary>
    /// Runs the first stage with <paramref name="html"/> as the
    /// source. When no rewrite happens we materialize a single byte[]
    /// copy so subsequent stages have a uniform
    /// <see cref="ReadOnlyMemory{T}"/> source.
    /// </summary>
    /// <param name="html">Original HTML span.</param>
    /// <param name="slots">Ping-pong buffer pair.</param>
    /// <param name="stage">Stage delegate.</param>
    /// <returns>Memory view of the current pipeline output.</returns>
    private static ReadOnlyMemory<byte> RunFirstStage(ReadOnlySpan<byte> html, ref StageBuffers slots, ByteStage stage)
    {
        slots.Spare.ResetWrittenCount();
        if (!stage(html, slots.Spare))
        {
            // No rewrite — materialize html as memory once so subsequent
            // stages don't need a ReadOnlySpan-vs-Memory branch.
            return new([.. html]);
        }

        var output = slots.Spare.WrittenMemory;
        slots = slots.Swap();
        return output;
    }

    /// <summary>Runs a follow-on stage; the working source is already <see cref="ReadOnlyMemory{T}"/>-rooted.</summary>
    /// <param name="source">Pipeline source.</param>
    /// <param name="slots">Ping-pong buffer pair.</param>
    /// <param name="stage">Stage delegate.</param>
    /// <returns>Memory view of the current pipeline output.</returns>
    private static ReadOnlyMemory<byte> RunStage(in ReadOnlyMemory<byte> source, ref StageBuffers slots, ByteStage stage)
    {
        slots.Spare.ResetWrittenCount();
        if (!stage(source.Span, slots.Spare))
        {
            return source;
        }

        var output = slots.Spare.WrittenMemory;
        slots = slots.Swap();
        return output;
    }

    /// <summary>Ping-pong buffer pair — <see cref="Spare"/> is the next stage's target; <see cref="Other"/> holds the prior output (or is empty on the first call).</summary>
    /// <param name="Spare">Buffer the next stage writes into.</param>
    /// <param name="Other">Buffer holding the prior stage's output (or unused before any stage has rewritten).</param>
    private readonly record struct StageBuffers(ArrayBufferWriter<byte> Spare, ArrayBufferWriter<byte> Other)
    {
        /// <summary>Swaps the two buffers; called after a stage commits to a rewrite so the next stage writes into the freed buffer.</summary>
        /// <returns>The swapped pair.</returns>
        [SuppressMessage(
            "SonarAnalyzer",
            "S2234:Parameters should be passed in the correct order",
            Justification = "Swap intentionally reverses the pair — the prior Other becomes the new Spare and vice versa.")]
        public StageBuffers Swap() => new(Other, Spare);
    }
}
