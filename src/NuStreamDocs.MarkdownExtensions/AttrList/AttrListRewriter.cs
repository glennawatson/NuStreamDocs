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
        || html.IndexOf("{."u8) >= 0;

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
        var slots = new StageBuffers(rentalA.Writer, rentalB.Writer);

        // Stage 1 reads the original html span directly; subsequent stages
        // read from whichever two-buffer buffer holds the latest output.
        var current = RunFirstStage(html, ref slots, InlinePairedAttrListBytes.RewriteInto);
        current = RunStage(current, ref slots, InlineVoidAttrListBytes.RewriteInto);
        current = RunStage(current, ref slots, BlockAttrListBytes.RewriteInto);

        sink.Write(current.Span);
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
            return html.ToArray();
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
