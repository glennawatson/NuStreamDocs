// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Common;

/// <summary>Byte-level UTF-8 helpers for ASCII case folding, identifier / word-boundary detection, whitespace handling, and string emit.</summary>
public static class AsciiByteHelpers
{
    /// <summary>The ASCII case-fold bit.</summary>
    private const byte CaseBit = 0x20;

    /// <summary>Gets the ASCII bit that distinguishes uppercase letters from lowercase letters; OR-ing with this folds case for ASCII letters.</summary>
    public static byte AsciiCaseBit => CaseBit;

    /// <summary>Returns true when <paramref name="b"/> contributes to an ASCII identifier (letter / digit / underscore).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for identifier bytes.</returns>
    public static bool IsAsciiIdentifierByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'a' and <= (byte)'z'
            or >= (byte)'0' and <= (byte)'9'
            or (byte)'_';

    /// <summary>Returns true when <paramref name="b"/> may appear inside a slug / kebab token (identifier byte or <c>-</c>).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for slug bytes (letter / digit / <c>_</c> / <c>-</c>).</returns>
    public static bool IsAsciiSlugByte(byte b) =>
        IsAsciiIdentifierByte(b) || b is (byte)'-';

    /// <summary>True when <paramref name="b"/> is an ASCII letter (<c>A</c>–<c>Z</c>, <c>a</c>–<c>z</c>).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for ASCII letters.</returns>
    public static bool IsAsciiLetter(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

    /// <summary>True when <paramref name="b"/> is an ASCII decimal digit (<c>0</c>–<c>9</c>).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for ASCII digits.</returns>
    public static bool IsAsciiDigit(byte b) =>
        b is >= (byte)'0' and <= (byte)'9';

    /// <summary>True when <paramref name="b"/> is an ASCII hex digit (<c>0</c>–<c>9</c>, <c>A</c>–<c>F</c>, <c>a</c>–<c>f</c>).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for ASCII hex digits.</returns>
    public static bool IsAsciiHexDigit(byte b) =>
        b is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f';

    /// <summary>True when every byte in <paramref name="bytes"/> is ASCII whitespace, or the span is empty.</summary>
    /// <param name="bytes">UTF-8 source.</param>
    /// <returns>True for empty or whitespace-only spans.</returns>
    public static bool IsAllAsciiWhitespace(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!IsAsciiWhitespace(bytes[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns true when <paramref name="b"/> is ASCII whitespace.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for whitespace.</returns>
    public static bool IsAsciiWhitespace(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    /// <summary>True for ASCII horizontal whitespace (space or tab) — newlines do NOT count.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for space or tab.</returns>
    public static bool IsAsciiHorizontalWhitespace(byte b) => b is (byte)' ' or (byte)'\t';

    /// <summary>Skips ASCII horizontal whitespace (space + tab) from <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Starting offset.</param>
    /// <returns>Offset of the first non-horizontal-whitespace byte.</returns>
    public static int SkipHorizontalWhitespace(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && IsAsciiHorizontalWhitespace(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Trims leading + trailing ASCII horizontal whitespace (space + tab) bytes from <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 source.</param>
    /// <returns>Trimmed slice; empty when <paramref name="bytes"/> contains only horizontal whitespace.</returns>
    public static ReadOnlySpan<byte> TrimHorizontalWhitespace(ReadOnlySpan<byte> bytes)
    {
        var start = 0;
        while (start < bytes.Length && IsAsciiHorizontalWhitespace(bytes[start]))
        {
            start++;
        }

        var end = bytes.Length;
        while (end > start && IsAsciiHorizontalWhitespace(bytes[end - 1]))
        {
            end--;
        }

        return bytes[start..end];
    }

    /// <summary>Folds a single ASCII letter to lowercase; non-letters pass through unchanged.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>Lowercased byte.</returns>
    public static byte ToAsciiLowerByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b | AsciiCaseBit) : b;

    /// <summary>Folds a single ASCII letter to a lowercase <see cref="char"/>; non-letters pass through unchanged.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>Lowercased character.</returns>
    public static char ToAsciiLowerChar(byte b) =>
        (char)ToAsciiLowerByte(b);

    /// <summary>Returns true when offset <paramref name="offset"/> in <paramref name="source"/> is at an ASCII <c>\b</c> word boundary.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <returns>True when the byte before <paramref name="offset"/> is not an identifier byte.</returns>
    public static bool IsWordBoundary(ReadOnlySpan<byte> source, int offset) =>
        offset is 0 || !IsAsciiIdentifierByte(source[offset - 1]);

    /// <summary>Counts the maximal run of <paramref name="marker"/> bytes starting at <paramref name="pos"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Run start.</param>
    /// <param name="marker">Byte to count.</param>
    /// <returns>Run length in bytes; zero when <paramref name="pos"/> is at or past the end, or when <paramref name="source"/>[<paramref name="pos"/>] is not <paramref name="marker"/>.</returns>
    public static int RunLength(ReadOnlySpan<byte> source, int pos, byte marker)
    {
        var i = pos;
        while (i < source.Length && source[i] == marker)
        {
            i++;
        }

        return i - pos;
    }

    /// <summary>Skips ASCII whitespace from <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Starting offset.</param>
    /// <returns>Offset of the first non-whitespace byte.</returns>
    public static int SkipWhitespace(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && IsAsciiWhitespace(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Trims leading + trailing ASCII whitespace bytes from <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 source.</param>
    /// <returns>Trimmed slice; empty when <paramref name="bytes"/> contains only whitespace.</returns>
    public static ReadOnlySpan<byte> TrimAsciiWhitespace(ReadOnlySpan<byte> bytes)
    {
        var start = 0;
        while (start < bytes.Length && IsAsciiWhitespace(bytes[start]))
        {
            start++;
        }

        var end = bytes.Length;
        while (end > start && IsAsciiWhitespace(bytes[end - 1]))
        {
            end--;
        }

        return bytes[start..end];
    }

    /// <summary>Strips a trailing CR/LF terminator from <paramref name="line"/>.</summary>
    /// <param name="line">Candidate line.</param>
    /// <returns>The line without its trailing newline; <paramref name="line"/> verbatim when no terminator is present.</returns>
    public static ReadOnlySpan<byte> TrimTrailingNewline(ReadOnlySpan<byte> line)
    {
        var end = line.Length;
        if (end > 0 && line[end - 1] is (byte)'\n')
        {
            end--;
        }

        if (end > 0 && line[end - 1] is (byte)'\r')
        {
            end--;
        }

        return line[..end];
    }

    /// <summary>
    /// Case-insensitive ASCII byte-prefix match at <paramref name="offset"/>.
    /// <paramref name="lowerCase"/> must already be lowercased and contain only ASCII letters or
    /// bytes whose bit-5 is set in their lowercase form (digits, most ASCII punctuation excluding <c>_</c>).
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <param name="lowerCase">Lowercase ASCII bytes to compare against.</param>
    /// <returns>True when the bytes at <paramref name="offset"/> equal <paramref name="lowerCase"/> ignoring ASCII case.</returns>
    public static bool StartsWithIgnoreAsciiCase(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> lowerCase)
    {
        if (offset + lowerCase.Length > source.Length)
        {
            return false;
        }

        for (var i = 0; i < lowerCase.Length; i++)
        {
            var b = source[offset + i];
            if (b is >= (byte)'A' and <= (byte)'Z')
            {
                b |= AsciiCaseBit;
            }

            if (b != lowerCase[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Case-insensitive ASCII equality test over the full spans (lengths must match). Same lowercase-reference contract as <see cref="StartsWithIgnoreAsciiCase"/>.</summary>
    /// <param name="a">Span to test.</param>
    /// <param name="b">Lowercase ASCII span to compare against.</param>
    /// <returns>True when equal ignoring ASCII case.</returns>
    /// <remarks>
    /// Folds only <c>A</c>-<c>Z</c> on <paramref name="a"/> (callers must supply <paramref name="b"/>
    /// already-lowercased); other bytes compare ordinally so identifiers containing <c>_</c>, <c>-</c>,
    /// or digits round-trip correctly.
    /// </remarks>
    public static bool EqualsIgnoreAsciiCase(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            var ai = a[i];
            if (ai is >= (byte)'A' and <= (byte)'Z')
            {
                ai |= AsciiCaseBit;
            }

            if (ai != b[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Case-insensitive ASCII tri-state compare over <paramref name="a"/> and <paramref name="b"/>.</summary>
    /// <param name="a">Left span.</param>
    /// <param name="b">Right span.</param>
    /// <returns>Negative, zero, or positive per <see cref="IComparer{T}"/>.</returns>
    /// <remarks>Not locale-aware; suitable for sorting English-language UTF-8 titles.</remarks>
    public static int CompareIgnoreAsciiCase(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var min = a.Length < b.Length ? a.Length : b.Length;
        for (var i = 0; i < min; i++)
        {
            var ai = a[i];
            var bi = b[i];
            if (ai is >= (byte)'A' and <= (byte)'Z')
            {
                ai |= AsciiCaseBit;
            }

            if (bi is >= (byte)'A' and <= (byte)'Z')
            {
                bi |= AsciiCaseBit;
            }

            if (ai != bi)
            {
                return ai - bi;
            }
        }

        return a.Length - b.Length;
    }

    /// <summary>Encodes <paramref name="value"/> into <paramref name="sink"/> as UTF-8 with no intermediate buffer.</summary>
    /// <param name="value">Source string.</param>
    /// <param name="sink">UTF-8 sink.</param>
    public static void EncodeStringInto(string value, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(sink);
        if (value.Length is 0)
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var dst = sink.GetSpan(max);
        var written = Encoding.UTF8.GetBytes(value, dst);
        sink.Advance(written);
    }

    /// <summary>ASCII-lowercases <paramref name="value"/> into a fresh byte array.</summary>
    /// <param name="value">UTF-8 input; non-ASCII bytes are preserved verbatim.</param>
    /// <returns>Fresh byte array, same length as <paramref name="value"/>.</returns>
    /// <remarks>
    /// Folds only <c>A</c>-<c>Z</c>; non-ASCII bytes (including UTF-8 continuation bytes) pass
    /// through unchanged. Suitable for ASCII-only identifiers — language ids, HTML attribute
    /// names, host names.
    /// </remarks>
    public static byte[] ToLowerCaseInvariant(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return [];
        }

        var result = new byte[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            result[i] = b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b | AsciiCaseBit) : b;
        }

        return result;
    }
}
