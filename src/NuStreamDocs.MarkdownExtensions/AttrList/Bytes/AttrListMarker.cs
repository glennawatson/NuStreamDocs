// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Helpers for locating the <c>{: ... }</c> attr-list marker in UTF-8
/// HTML and for emitting the rewritten opening tag once a parsed
/// attribute set is in hand.
/// </summary>
internal static class AttrListMarker
{
    /// <summary>Gets the UTF-8 bytes for the opening <c>{:</c> marker.</summary>
    public static ReadOnlySpan<byte> OpenMarker => "{:"u8;

    /// <summary>Tries to match an optional-whitespace + <c>{: ... }</c> token starting exactly at <paramref name="p"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset (typically just after the closing <c>&gt;</c> or <c>&lt;/tag&gt;</c>).</param>
    /// <param name="contentStart">First byte of the inner attr-list text on success.</param>
    /// <param name="contentEnd">Offset of the closing <c>}</c> on success.</param>
    /// <param name="markerEnd">Offset just past the closing <c>}</c> on success.</param>
    /// <returns>True when a well-formed marker was found.</returns>
    public static bool TryMatchMarker(ReadOnlySpan<byte> source, int p, out int contentStart, out int contentEnd, out int markerEnd)
    {
        contentStart = -1;
        contentEnd = -1;
        markerEnd = -1;

        var afterWs = SkipAsciiWhitespace(source, p);
        if (afterWs + OpenMarker.Length >= source.Length || !source[afterWs..].StartsWith(OpenMarker))
        {
            return false;
        }

        var inside = SkipAsciiWhitespace(source, afterWs + OpenMarker.Length);
        var closeRel = source[inside..].IndexOf((byte)'}');
        if (closeRel < 0)
        {
            return false;
        }

        var close = inside + closeRel;
        contentStart = inside;
        contentEnd = TrimTrailingWhitespace(source, inside, close);
        markerEnd = close + 1;
        return contentEnd >= contentStart;
    }

    /// <summary>Decodes the inner attr-list text and runs it through <see cref="AttrListParser"/> + <see cref="AttrListMerger"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="existingAttrsStart">Offset of the existing attribute fragment.</param>
    /// <param name="existingAttrsEnd">Offset just past the existing attribute fragment.</param>
    /// <param name="attrListStart">Offset of the inner attr-list text.</param>
    /// <param name="attrListEnd">Offset just past the inner attr-list text.</param>
    /// <returns>Merged attribute fragment (with leading space, possibly empty).</returns>
    public static string ParseAndMerge(ReadOnlySpan<byte> source, int existingAttrsStart, int existingAttrsEnd, int attrListStart, int attrListEnd)
    {
        var existing = Encoding.UTF8.GetString(source[existingAttrsStart..existingAttrsEnd]);
        var attrsText = Encoding.UTF8.GetString(source[attrListStart..attrListEnd]);
        var (id, classes, kv) = AttrListParser.Parse(attrsText);
        return AttrListMerger.Merge(existing, id, classes, kv);
    }

    /// <summary>Writes a UTF-16 string as UTF-8 bytes to the sink.</summary>
    /// <param name="value">String to encode.</param>
    /// <param name="sink">UTF-8 sink.</param>
    public static void WriteString(string value, IBufferWriter<byte> sink)
    {
        if (value.Length is 0)
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var dst = sink.GetSpan(max);
        var written = Encoding.UTF8.GetBytes(value, dst);
        sink.Advance(written);
    }

    /// <summary>Skips ASCII whitespace from <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset.</param>
    /// <returns>Offset of the first non-whitespace byte.</returns>
    private static int SkipAsciiWhitespace(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && source[p] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            p++;
        }

        return p;
    }

    /// <summary>Trims trailing whitespace bytes from a range.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Start offset (inclusive).</param>
    /// <param name="end">End offset (exclusive) — typically the offset of the close byte.</param>
    /// <returns>End offset trimmed of trailing whitespace.</returns>
    private static int TrimTrailingWhitespace(ReadOnlySpan<byte> source, int start, int end)
    {
        var p = end;
        while (p > start && source[p - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            p--;
        }

        return p;
    }
}
