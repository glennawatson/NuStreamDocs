// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.Internal;

/// <summary>
/// Shared opener-line parser for admonitions, details, and tabbed —
/// they all parse a leading marker, a type/identifier token,
/// optional whitespace, an optional <c>"…"</c> title, then a line
/// terminator.
/// </summary>
internal static class OpenerLineParser
{
    /// <summary>ASCII offset between lowercase and uppercase letters.</summary>
    private const int LowerToUpperOffset = 32;

    /// <summary>Tries to consume an optional <c>"title"</c> token at <paramref name="p"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="p">Cursor; advanced past the closing quote on success.</param>
    /// <param name="titleStart">Set to the title-token offset when present.</param>
    /// <param name="titleLen">Set to the title-token length when present.</param>
    /// <returns>True when no title is present or a complete <c>"…"</c> token was parsed.</returns>
    public static bool TryParseTitle(ReadOnlySpan<byte> source, ref int p, out int titleStart, out int titleLen)
    {
        titleStart = 0;
        titleLen = 0;
        if (p >= source.Length || source[p] != (byte)'"')
        {
            return true;
        }

        p++;
        titleStart = p;
        while (p < source.Length && source[p] != (byte)'"' && source[p] != (byte)'\n')
        {
            p++;
        }

        if (p >= source.Length || source[p] != (byte)'"')
        {
            return false;
        }

        titleLen = p - titleStart;
        p++;
        return true;
    }

    /// <summary>Advances <paramref name="offset"/> while <paramref name="predicate"/> matches.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Starting offset.</param>
    /// <param name="predicate">Byte-class predicate.</param>
    /// <returns>The first offset where the predicate fails.</returns>
    public static int ScanWhile(ReadOnlySpan<byte> source, int offset, Func<byte, bool> predicate)
    {
        var p = offset;
        while (p < source.Length && predicate(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Returns true for ASCII identifier characters allowed in a type token.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when the byte may appear inside a type token.</returns>
    public static bool IsTypeChar(byte b) =>
        b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'_';

    /// <summary>Returns true for ASCII space or tab.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when horizontal whitespace.</returns>
    public static bool IsHorizontalSpace(byte b) => b is (byte)' ' or (byte)'\t';

    /// <summary>Returns true for trailing header bytes — horizontal whitespace plus CR.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when allowed trailing the opener.</returns>
    public static bool IsTrailingHeaderByte(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r';

    /// <summary>
    /// Parses the post-marker tail of an admonition / details opener:
    /// type token, optional whitespace, optional <c>"…"</c> title,
    /// trailing whitespace/CR, then a newline (or EOF).
    /// </summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="markerEnd">Offset just past the opener marker (e.g. <c>!!! </c>).</param>
    /// <param name="typeStart">Offset of the type token on success.</param>
    /// <param name="typeLen">Length of the type token on success.</param>
    /// <param name="titleStart">Offset of the title token (zero when absent).</param>
    /// <param name="titleLen">Length of the title token (zero when absent).</param>
    /// <param name="headerEnd">Offset just past the opener line's terminator on success.</param>
    /// <returns>True when the opener tail is well-formed.</returns>
    public static bool TryParseTypeAndTitle(
        ReadOnlySpan<byte> source,
        int markerEnd,
        out int typeStart,
        out int typeLen,
        out int titleStart,
        out int titleLen,
        out int headerEnd)
    {
        typeStart = markerEnd;
        typeLen = 0;
        titleStart = 0;
        titleLen = 0;
        headerEnd = 0;

        var p = ScanWhile(source, markerEnd, IsTypeChar);
        typeLen = p - markerEnd;
        if (typeLen is 0)
        {
            return false;
        }

        p = ScanWhile(source, p, IsHorizontalSpace);
        if (!TryParseTitle(source, ref p, out titleStart, out titleLen))
        {
            return false;
        }

        p = ScanWhile(source, p, IsTrailingHeaderByte);
        if (p < source.Length && source[p] != (byte)'\n')
        {
            return false;
        }

        headerEnd = p < source.Length ? p + 1 : p;
        return true;
    }

    /// <summary>Title-cases <paramref name="type"/> by uppercasing the first ASCII letter and writes it.</summary>
    /// <param name="type">UTF-8 type token; an empty span writes nothing.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void WriteTitleCase(ReadOnlySpan<byte> type, IBufferWriter<byte> writer)
    {
        if (type.Length is 0)
        {
            return;
        }

        var dest = writer.GetSpan(type.Length);
        type.CopyTo(dest);
        if (dest[0] is >= (byte)'a' and <= (byte)'z')
        {
            dest[0] = (byte)(dest[0] - LowerToUpperOffset);
        }

        writer.Advance(type.Length);
    }
}
