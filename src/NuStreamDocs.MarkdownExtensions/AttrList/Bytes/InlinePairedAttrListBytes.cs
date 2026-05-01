// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Byte-level scanner for the paired-inline attr-list pattern —
/// <c>&lt;tag attrs&gt;inner&lt;/tag&gt;{: attrs }</c>. Replaces
/// <c>InlinePairedAttrListRegex</c>.
/// </summary>
internal static class InlinePairedAttrListBytes
{
    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every paired-inline element followed by a <c>{: ... }</c> token.</summary>
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
            if (!AttrListTagMatcher.TryMatchInlinePairedTag(html, lt + 1, out var nameLen))
            {
                advanceTo = lt + 1;
                return false;
            }

            var nameEnd = lt + 1 + nameLen;
            var openGt = AttrListTagScanner.FindFirst(html, nameEnd, (byte)'>');
            if (openGt < 0)
            {
                advanceTo = lt + 1;
                return false;
            }

            var innerStart = openGt + 1;
            var closeStart = AttrListTagScanner.FindMatchingClose(html, innerStart, html.Slice(lt + 1, nameLen));
            if (closeStart < 0)
            {
                advanceTo = innerStart;
                return false;
            }

            var afterClose = closeStart + nameLen + AttrListTagScanner.CloseTagOverhead;
            if (!AttrListMarker.TryMatchMarker(html, afterClose, out var contentStart, out var contentEnd, out var markerEnd))
            {
                advanceTo = afterClose;
                return false;
            }

            var merged = AttrListMarker.ParseAndMerge(html, nameEnd, openGt, contentStart, contentEnd);
            sink.Write(html[lastEmit..nameEnd]);
            AttrListMarker.WriteString(merged, sink);
            sink.Write(html[openGt..afterClose]);
            lastEmit = markerEnd;
            advanceTo = markerEnd;
            return true;
        }
    }
}
