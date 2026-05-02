// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>Shared static attr-list rewrite helpers used by the concrete scanners.</summary>
internal static class AttrListElementRewriter
{
    /// <summary>Tries to rewrite an inline paired tag followed by a trailing marker.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="lt">Offset of the opening <c>&lt;</c>.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to (updated on success).</param>
    /// <param name="advanceTo">Next scan cursor.</param>
    /// <returns>True when a rewrite succeeded.</returns>
    public static bool TryRewriteInlinePaired(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
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

        sink.Write(html[lastEmit..nameEnd]);
        AttrListMarker.EmitMerged(html, nameEnd, openGt, contentStart, contentEnd, sink);
        sink.Write(html[openGt..afterClose]);
        lastEmit = markerEnd;
        advanceTo = markerEnd;
        return true;
    }

    /// <summary>Tries to rewrite a block tag whose body contains an inline marker.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="lt">Offset of the opening <c>&lt;</c>.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to (updated on success).</param>
    /// <param name="advanceTo">Next scan cursor.</param>
    /// <returns>True when a rewrite succeeded.</returns>
    public static bool TryRewriteBlock(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!AttrListTagMatcher.TryMatchBlockTag(html, lt + 1, out var nameLen))
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
        var tagName = html.Slice(lt + 1, nameLen);
        var closeStart = AttrListTagScanner.FindMatchingClose(html, innerStart, tagName);
        if (closeStart < 0)
        {
            advanceTo = innerStart;
            return false;
        }

        if (!TryFindMarkerInside(html, innerStart, closeStart, out var prefixEnd, out var contentStart, out var contentEnd, out var suffixStart))
        {
            advanceTo = closeStart + nameLen + AttrListTagScanner.CloseTagOverhead;
            return false;
        }

        EmitRewrittenBlock(html, sink, ref lastEmit, new(nameEnd, openGt, prefixEnd, contentStart, contentEnd, suffixStart, closeStart, nameLen));
        advanceTo = closeStart + nameLen + AttrListTagScanner.CloseTagOverhead;
        return true;
    }

    /// <summary>Tries to rewrite a void inline tag followed by a trailing marker.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="lt">Offset of the opening <c>&lt;</c>.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to (updated on success).</param>
    /// <param name="advanceTo">Next scan cursor.</param>
    /// <returns>True when a rewrite succeeded.</returns>
    public static bool TryRewriteInlineVoid(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
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

        sink.Write(html[lastEmit..nameEnd]);
        AttrListMarker.EmitMerged(html, nameEnd, slashStart, contentStart, contentEnd, sink);
        sink.Write(html[slashStart..(gt + 1)]);
        lastEmit = markerEnd;
        advanceTo = markerEnd;
        return true;
    }

    /// <summary>Emits the rewritten block element into the sink.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to (updated).</param>
    /// <param name="match">Match positions for the block rewrite.</param>
    private static void EmitRewrittenBlock(ReadOnlySpan<byte> html, IBufferWriter<byte> sink, ref int lastEmit, in BlockMatch match)
    {
        sink.Write(html[lastEmit..match.NameEnd]);
        AttrListMarker.EmitMerged(html, match.NameEnd, match.OpenGt, match.ContentStart, match.ContentEnd, sink);
        var openGtEnd = match.OpenGt + 1;
        sink.Write(html[match.OpenGt..openGtEnd]);
        sink.Write(html[openGtEnd..match.PrefixEnd]);
        sink.Write(html[match.SuffixStart..match.CloseStart]);
        var closeEnd = match.CloseStart + match.NameLen + AttrListTagScanner.CloseTagOverhead;
        sink.Write(html[match.CloseStart..closeEnd]);
        lastEmit = closeEnd;
    }

    /// <summary>Returns the offset of the start of the <c>\s*/</c> run before <c>&gt;</c>, or <paramref name="gt"/> when there is no slash directly before <c>&gt;</c>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="nameEnd">Offset just past the tag name.</param>
    /// <param name="gt">Offset of the closing <c>&gt;</c>.</param>
    /// <returns>Offset of the slash run, or <paramref name="gt"/> when absent.</returns>
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

    /// <summary>Locates a <c>{: ... }</c> token inside a block body, splitting it into prefix / marker / suffix slices.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="innerStart">Inner-content start offset.</param>
    /// <param name="closeStart">Offset of the closing tag's <c>&lt;</c>.</param>
    /// <param name="prefixEnd">Trimmed prefix end on success.</param>
    /// <param name="contentStart">Marker-content start on success.</param>
    /// <param name="contentEnd">Marker-content end on success.</param>
    /// <param name="suffixStart">Suffix start on success.</param>
    /// <returns>True when a legal inline marker was found inside the block body.</returns>
    private static bool TryFindMarkerInside(ReadOnlySpan<byte> html, int innerStart, int closeStart, out int prefixEnd, out int contentStart, out int contentEnd, out int suffixStart)
    {
        prefixEnd = -1;
        contentStart = -1;
        contentEnd = -1;
        suffixStart = -1;

        var inner = html[innerStart..closeStart];
        var rel = inner.IndexOf(AttrListMarker.OpenMarker);
        if (rel < 0)
        {
            return false;
        }

        var openMarker = innerStart + rel;
        var trimmedPrefixEnd = openMarker;
        while (trimmedPrefixEnd > innerStart && html[trimmedPrefixEnd - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            trimmedPrefixEnd--;
        }

        if (!AttrListMarker.TryMatchMarker(html, openMarker, out contentStart, out contentEnd, out var markerEnd))
        {
            return false;
        }

        prefixEnd = trimmedPrefixEnd;
        var suffix = html[markerEnd..closeStart];
        if (suffix.IndexOf((byte)'<') >= 0)
        {
            return false;
        }

        suffixStart = markerEnd;
        return true;
    }
}
