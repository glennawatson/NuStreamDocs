// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

namespace NuStreamDocs.MarkdownExtensions.AttrList;

/// <summary>
/// Stateless HTML post-pass that lifts <c>{: ... }</c> tokens out of
/// the rendered HTML and into attributes on the matching opening tag.
/// </summary>
/// <remarks>
/// Three patterns: block (<c>&lt;hN&gt;Heading {: .x }&lt;/hN&gt;</c>),
/// paired inline (<c>&lt;a href="…"&gt;text&lt;/a&gt;{: .x }</c>), and
/// void inline (<c>&lt;img src="…"&gt;{: .x }</c>). Each pattern has a
/// dedicated byte-level scanner under <see cref="Bytes"/> that walks
/// the UTF-8 buffer once and only allocates per match.
/// </remarks>
internal static class AttrListRewriter
{
    /// <summary>Delegate matching the byte-level scanner signature for one of the three passes.</summary>
    /// <param name="source">UTF-8 source span.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one rewrite happened.</returns>
    private delegate bool ByteStage(ReadOnlySpan<byte> source, IBufferWriter<byte> sink);

    /// <summary>Gets the UTF-8 marker the text must contain for any rewrite to happen.</summary>
    private static ReadOnlySpan<byte> Marker => "{:"u8;

    /// <summary>Returns true when <paramref name="html"/> contains at least one <c>{:</c> marker.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <returns>True when a candidate exists.</returns>
    public static bool NeedsRewrite(ReadOnlySpan<byte> html) => html.IndexOf(Marker) >= 0;

    /// <summary>Rewrites every block- and inline-level attr-list token in <paramref name="html"/> directly into <paramref name="sink"/>.</summary>
    /// <param name="html">Page HTML span.</param>
    /// <param name="sink">UTF-8 sink the rewritten HTML is encoded into.</param>
    public static void RewriteInto(ReadOnlySpan<byte> html, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        // Inline passes run first so a {: } sitting between an inline
        // element and a closing block tag isn't swallowed by the block pattern.
        var current = html.ToArray();
        current = RunStage(current, InlinePairedAttrListBytes.RewriteInto);
        current = RunStage(current, InlineVoidAttrListBytes.RewriteInto);
        current = RunStage(current, BlockAttrListBytes.RewriteInto);
        sink.Write(current);
    }

    /// <summary>Runs one byte-level stage; returns the same buffer reference when nothing rewrote.</summary>
    /// <param name="source">UTF-8 buffer.</param>
    /// <param name="stage">Stage to run.</param>
    /// <returns>The current buffer (fresh array when rewritten, the input array otherwise).</returns>
    private static byte[] RunStage(byte[] source, ByteStage stage)
    {
        var sink = new ArrayBufferWriter<byte>(source.Length);
        return stage(source, sink) ? sink.WrittenSpan.ToArray() : source;
    }
}
