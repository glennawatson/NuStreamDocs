// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.Html;

/// <summary>
/// UTF-8 HTML escaper.
/// </summary>
/// <remarks>
/// Operates entirely on bytes — no string/UTF-16 round trip. The hot
/// path advances over runs of unescaped bytes via vectorized
/// <c>IndexOfAny(SearchValues&lt;byte&gt;)</c> and writes them in bulk to
/// the destination, falling through to a per-byte switch only at
/// escape points.
/// </remarks>
public static class HtmlEscape
{
    /// <summary>Bytes that require an HTML entity replacement in text content.</summary>
    private static readonly SearchValues<byte> EscapeBytes =
        SearchValues.Create("&<>\""u8);

    /// <summary>Bytes that require an HTML entity replacement inside a double-quoted attribute value.</summary>
    /// <remarks>Only <c>&amp;</c> and <c>"</c> need escaping; <c>&lt;</c> and <c>&gt;</c> are literal in attribute values.</remarks>
    private static readonly SearchValues<byte> AttributeEscapeBytes =
        SearchValues.Create("&\""u8);

    /// <summary>Chars that require an HTML entity replacement in text content (UTF-16 form of <see cref="EscapeBytes"/>).</summary>
    private static readonly SearchValues<char> EscapeChars =
        SearchValues.Create("&<>\"");

    /// <summary>
    /// Writes <paramref name="source"/> to <paramref name="writer"/>,
    /// replacing <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, and <c>&quot;</c>
    /// with their named HTML entities.
    /// </summary>
    /// <param name="source">UTF-8 input span.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void EscapeText(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (source.IsEmpty)
        {
            return;
        }

        // Hint the writer to fit at least the source bytes verbatim — the
        // common case (no escapable bytes) writes exactly source.Length and
        // a writer pre-sized by the caller skips the grow entirely. The
        // previous worst-case 6× hint forced a multi-hundred-KB upfront
        // allocation on every clean page. The per-byte WriteEntity loop
        // grows the buffer further on the heavy path naturally.
        _ = writer.GetSpan(source.Length);

        var cursor = source;
        while (!cursor.IsEmpty)
        {
            var idx = cursor.IndexOfAny(EscapeBytes);
            switch (idx)
            {
                case < 0:
                    {
                        CopyTo(cursor, writer);
                        return;
                    }

                case > 0:
                    {
                        CopyTo(cursor[..idx], writer);
                        break;
                    }
            }

            // Pass through pre-formed HTML entity references (`&#dddd;`, `&#xhhhh;`, `&name;`)
            // verbatim — re-escaping them would emit literal `&amp;#x2714;` in output.
            if (cursor[idx] is (byte)'&' && IsEntityReference(cursor[idx..], out var entityLen))
            {
                CopyTo(cursor.Slice(idx, entityLen), writer);
                cursor = cursor[(idx + entityLen)..];
                continue;
            }

            WriteEntity(cursor[idx], writer);
            cursor = cursor[(idx + 1)..];
        }
    }

    /// <summary>
    /// UTF-16 overload that writes the escaped, UTF-8-encoded form of
    /// <paramref name="source"/> straight into <paramref name="writer"/>.
    /// Same entity replacements as the byte overload — but no
    /// intermediate buffer is rented, so per-call allocations stay at
    /// zero on the highlight hot path.
    /// </summary>
    /// <param name="source">UTF-16 input span.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void EscapeText(in ReadOnlySpan<char> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (source.IsEmpty)
        {
            return;
        }

        var cursor = source;
        while (!cursor.IsEmpty)
        {
            var idx = cursor.IndexOfAny(EscapeChars);
            switch (idx)
            {
                case < 0:
                    {
                        EncodeUtf8(cursor, writer);
                        return;
                    }

                case > 0:
                    {
                        EncodeUtf8(cursor[..idx], writer);
                        break;
                    }
            }

            WriteEntity((byte)cursor[idx], writer);
            cursor = cursor[(idx + 1)..];
        }
    }

    /// <summary>Writes <paramref name="source"/> to <paramref name="writer"/>, escaping only <c>&amp;</c> and <c>"</c> for use inside a double-quoted HTML attribute value.</summary>
    /// <param name="source">UTF-8 input span.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <remarks>
    /// Sister to the byte <c>EscapeText</c> overload for attribute-value callers.
    /// <c>&lt;</c> and <c>&gt;</c> are literal inside attribute values, so escaping them is unnecessary
    /// and can corrupt values that carry markup-like content.
    /// </remarks>
    public static void EscapeAttribute(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (source.IsEmpty)
        {
            return;
        }

        var cursor = source;
        while (!cursor.IsEmpty)
        {
            var idx = cursor.IndexOfAny(AttributeEscapeBytes);
            switch (idx)
            {
                case < 0:
                    {
                        CopyTo(cursor, writer);
                        return;
                    }

                case > 0:
                    {
                        CopyTo(cursor[..idx], writer);
                        break;
                    }
            }

            CopyTo(cursor[idx] is (byte)'&' ? "&amp;"u8 : "&quot;"u8, writer);
            cursor = cursor[(idx + 1)..];
        }
    }

    /// <summary>Encodes a non-empty run of UTF-16 chars into <paramref name="writer"/> as UTF-8 with no intermediate buffer.</summary>
    /// <param name="chars">UTF-16 span (must be non-empty).</param>
    /// <param name="writer">UTF-8 sink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeUtf8(in ReadOnlySpan<char> chars, IBufferWriter<byte> writer)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(chars.Length);
        var dst = writer.GetSpan(maxBytes);
        var written = Encoding.UTF8.GetBytes(chars, dst);
        writer.Advance(written);
    }

    /// <summary>Bulk-copies <paramref name="src"/> into <paramref name="writer"/>.</summary>
    /// <param name="src">UTF-8 bytes to copy verbatim.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CopyTo(ReadOnlySpan<byte> src, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(src.Length);
        src.CopyTo(dst);
        writer.Advance(src.Length);
    }

    /// <summary>Writes the entity replacement for <paramref name="ch"/>.</summary>
    /// <param name="ch">One of <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&quot;</c>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteEntity(byte ch, IBufferWriter<byte> writer)
    {
        var entity = ch switch
        {
            (byte)'&' => "&amp;"u8,
            (byte)'<' => "&lt;"u8,
            (byte)'>' => "&gt;"u8,
            (byte)'"' => "&quot;"u8,
            _ => default,
        };

        if (entity.IsEmpty)
        {
            var dst = writer.GetSpan(1);
            dst[0] = ch;
            writer.Advance(1);
            return;
        }

        CopyTo(entity, writer);
    }

    /// <summary>Returns true when <paramref name="cursor"/> begins with a well-formed HTML entity reference.</summary>
    /// <param name="cursor">UTF-8 span starting at the candidate <c>&amp;</c>.</param>
    /// <param name="length">Length of the matched reference (including the trailing <c>;</c>) on success.</param>
    /// <returns>True when a reference shape was matched.</returns>
    private static bool IsEntityReference(ReadOnlySpan<byte> cursor, out int length)
    {
        length = 0;
        if (cursor.Length < 3 || cursor[0] is not (byte)'&')
        {
            return false;
        }

        var bodyEnd = cursor[1] is (byte)'#'
            ? ScanNumericEntityBody(cursor)
            : ScanNamedEntityBody(cursor);
        if (bodyEnd < 0 || bodyEnd >= cursor.Length || cursor[bodyEnd] is not (byte)';')
        {
            return false;
        }

        length = bodyEnd + 1;
        return true;
    }

    /// <summary>Scans the body of <c>&amp;#dddd</c> / <c>&amp;#xhhhh</c> and returns the end offset (position of the expected <c>;</c>) or -1 on no match.</summary>
    /// <param name="cursor">Span starting at <c>&amp;</c>; <c>cursor[1]</c> is already <c>#</c>.</param>
    /// <returns>End offset, or -1.</returns>
    private static int ScanNumericEntityBody(ReadOnlySpan<byte> cursor)
    {
        var i = 2;
        var hex = i < cursor.Length && cursor[i] is (byte)'x' or (byte)'X';
        if (hex)
        {
            i++;
        }

        var digitStart = i;
        while (i < cursor.Length)
        {
            var b = cursor[i];
            var isDigit = hex ? IsAsciiHexDigit(b) : b is >= (byte)'0' and <= (byte)'9';
            if (!isDigit)
            {
                break;
            }

            i++;
        }

        return i == digitStart ? -1 : i;
    }

    /// <summary>Scans the body of <c>&amp;name</c> and returns the end offset (position of the expected <c>;</c>) or -1 on no match.</summary>
    /// <param name="cursor">Span starting at <c>&amp;</c>; <c>cursor[1]</c> is the candidate name lead byte.</param>
    /// <returns>End offset, or -1.</returns>
    private static int ScanNamedEntityBody(ReadOnlySpan<byte> cursor)
    {
        if (!IsAsciiLetter(cursor[1]))
        {
            return -1;
        }

        var i = 2;
        while (i < cursor.Length && (IsAsciiLetter(cursor[i]) || cursor[i] is >= (byte)'0' and <= (byte)'9'))
        {
            i++;
        }

        return i;
    }

    /// <summary>True when <paramref name="b"/> is an ASCII hex digit.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for <c>0-9 / a-f / A-F</c>.</returns>
    private static bool IsAsciiHexDigit(byte b) =>
        b is >= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'f' or >= (byte)'A' and <= (byte)'F';

    /// <summary>True when <paramref name="b"/> is an ASCII letter.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for <c>a-z / A-Z</c>.</returns>
    private static bool IsAsciiLetter(byte b) =>
        b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z';
}
