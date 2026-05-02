// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>
/// Byte-writing helpers shared across the AGLC4 formatters. All public methods write directly to
/// a <see cref="IBufferWriter{T}"/> sink with no intermediate <see cref="string"/> allocation —
/// every <see cref="CitationEntry"/> and <see cref="PersonName"/> field is already byte-shaped,
/// so emit is a straight <see cref="ReadOnlySpan{T}.CopyTo(System.Span{T})"/> into the sink.
/// </summary>
internal static class Aglc4Writer
{
    /// <summary>Writes a UTF-8 byte literal to the sink.</summary>
    /// <param name="bytes">UTF-8 bytes (typically a <c>"..."u8</c> literal).</param>
    /// <param name="writer">Sink.</param>
    public static void WriteBytes(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Writes UTF-8 entry-field bytes straight to the sink — every <see cref="CitationEntry"/> field is byte-shaped, so this is the canonical write path.</summary>
    /// <param name="value">UTF-8 source bytes.</param>
    /// <param name="writer">Sink.</param>
    public static void WriteString(ReadOnlySpan<byte> value, IBufferWriter<byte> writer) =>
        WriteBytes(value, writer);

    /// <summary>Writes an integer as ASCII (invariant culture) directly to the sink via <see cref="Utf8Formatter"/>.</summary>
    /// <param name="value">Integer value.</param>
    /// <param name="writer">Sink.</param>
    public static void WriteInt(int value, IBufferWriter<byte> writer)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            return;
        }

        WriteBytes(buffer[..written], writer);
    }

    /// <summary>Writes a parenthesized year suffix when the year is non-zero (e.g. <c> (1992)</c>).</summary>
    /// <param name="year">Year value; 0 means unknown.</param>
    /// <param name="writer">Sink.</param>
    public static void WriteParenthesizedYear(int year, IBufferWriter<byte> writer)
    {
        if (year is 0)
        {
            return;
        }

        WriteBytes(" ("u8, writer);
        WriteInt(year, writer);
        WriteBytes(")"u8, writer);
    }

    /// <summary>Writes the AGLC4 author list — joined as <c>A, B and C</c>.</summary>
    /// <param name="authors">Author list.</param>
    /// <param name="writer">Sink.</param>
    public static void WriteAuthors(PersonName[] authors, IBufferWriter<byte> writer)
    {
        for (var i = 0; i < authors.Length; i++)
        {
            if (i > 0 && i == authors.Length - 1)
            {
                WriteBytes(" and "u8, writer);
            }
            else if (i > 0)
            {
                WriteBytes(", "u8, writer);
            }

            WriteName(authors[i], writer);
        }
    }

    /// <summary>Writes a single name — <c>Given Family</c> for personal names, the literal for institutional ones.</summary>
    /// <param name="name">Name.</param>
    /// <param name="writer">Sink.</param>
    public static void WriteName(PersonName name, IBufferWriter<byte> writer)
    {
        if (name.IsInstitutional)
        {
            WriteString(name.Literal, writer);
            return;
        }

        if (name.Given.Length is 0)
        {
            WriteString(name.Family, writer);
            return;
        }

        WriteString(name.Given, writer);
        WriteBytes(" "u8, writer);
        WriteString(name.Family, writer);
    }
}
