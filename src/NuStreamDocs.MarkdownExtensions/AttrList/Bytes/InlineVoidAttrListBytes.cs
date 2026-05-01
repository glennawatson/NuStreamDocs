// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Byte-level scanner for the void-inline attr-list pattern —
/// <c>&lt;img attrs[/]&gt;{: attrs }</c>. Replaces
/// <c>InlineVoidAttrListRegex</c>.
/// </summary>
internal static class InlineVoidAttrListBytes
{
    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every void inline element followed by a <c>{: ... }</c> token.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one element was rewritten.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, IBufferWriter<byte> sink) =>
        AttrListRewriteLoop.RewriteInto<Strategy>(html, sink);

    /// <summary>Static dispatch strategy for the shared scan loop.</summary>
    private readonly record struct Strategy : IAttrListRewriteStrategy<Strategy>
    {
        /// <inheritdoc/>
        public static bool TryRewriteAt(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
        {
            if (!AttrListTagMatcher.TryMatchInlineVoidTag(html, lt + 1, out var nameLen))
            {
                advanceTo = lt + 1;
                return false;
            }

            var nameEnd = lt + 1 + nameLen;
            var gt = AttrListTagScanner.FindFirst(html, nameEnd, (byte)'>');
            if (gt < 0)
            {
                advanceTo = lt + 1;
                return false;
            }

            var slashStart = FindSlashRunStart(html, nameEnd, gt);
            if (!AttrListMarker.TryMatchMarker(html, gt + 1, out var contentStart, out var contentEnd, out var markerEnd))
            {
                advanceTo = gt + 1;
                return false;
            }

            var merged = AttrListMarker.ParseAndMerge(html, nameEnd, slashStart, contentStart, contentEnd);
            sink.Write(html[lastEmit..nameEnd]);
            AttrListMarker.WriteString(merged, sink);
            sink.Write(html[slashStart..(gt + 1)]);
            lastEmit = markerEnd;
            advanceTo = markerEnd;
            return true;
        }

        /// <summary>Returns the offset of the start of the <c>\s*/</c> run before <c>&gt;</c>, or <paramref name="gt"/> when there's no <c>/</c> directly before <c>&gt;</c>.</summary>
        /// <param name="html">UTF-8 source.</param>
        /// <param name="nameEnd">Offset just past the tag name.</param>
        /// <param name="gt">Offset of the closing <c>&gt;</c>.</param>
        /// <returns>Offset where the <c>\s*/</c> run begins, or <paramref name="gt"/> when no slash is present.</returns>
        private static int FindSlashRunStart(ReadOnlySpan<byte> html, int nameEnd, int gt)
        {
            if (gt - 1 < nameEnd || html[gt - 1] is not (byte)'/')
            {
                return gt;
            }

            var ws = gt - 1;
            while (ws > nameEnd && html[ws - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                ws--;
            }

            return ws;
        }
    }
}
