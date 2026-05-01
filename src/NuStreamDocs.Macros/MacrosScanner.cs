// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Macros;

/// <summary>
/// Byte-level UTF-8 scanner that walks markdown source, copies bytes
/// through verbatim, skips fenced and inline code regions, and replaces
/// <c>{{ name }}</c> markers with values from a host-supplied lookup.
/// </summary>
internal static class MacrosScanner
{
    /// <summary>HTML escape rules for the EscapeHtml option (matches the canonical 5-entity set).</summary>
    private static readonly SearchValues<byte> EscapeChars = SearchValues.Create("&<>\"'"u8);

    /// <summary>Lookup callback signature: byte name → string value (true on hit).</summary>
    /// <param name="name">UTF-8 name bytes.</param>
    /// <param name="value">Resolved value on hit.</param>
    /// <returns>True when the lookup succeeded.</returns>
    public delegate bool Lookup(ReadOnlySpan<byte> name, out string value);

    /// <summary>Missing-name callback signature; called for each <c>{{ name }}</c> with no resolved value.</summary>
    /// <param name="name">UTF-8 name bytes.</param>
    public delegate void MissingCallback(ReadOnlySpan<byte> name);

    /// <summary>Gets the two-byte opening marker.</summary>
    private static ReadOnlySpan<byte> Open => "{{"u8;

    /// <summary>Gets the two-byte closing marker.</summary>
    private static ReadOnlySpan<byte> Close => "}}"u8;

    /// <summary>Walks <paramref name="source"/> and writes the substituted output to <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="lookup">Resolves a name to a value; returns false when the name is unknown.</param>
    /// <param name="escapeHtml">When true, resolved values are HTML-escaped before being written.</param>
    /// <param name="onMissing">Optional callback invoked for unknown names; <c>null</c> to silently leave them in place.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, Lookup lookup, bool escapeHtml, MissingCallback? onMissing, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(writer);

        var cursor = 0;
        while (cursor < source.Length)
        {
            cursor = ProcessOne(source, cursor, lookup, escapeHtml, onMissing, writer);
        }
    }

    /// <summary>Processes one segment: either a code region (passed through untouched) or a single non-code chunk where macros expand.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Current scan offset.</param>
    /// <param name="lookup">Lookup callback.</param>
    /// <param name="escapeHtml">Whether to HTML-escape resolved values.</param>
    /// <param name="onMissing">Missing-name callback.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Offset to resume from.</returns>
    private static int ProcessOne(ReadOnlySpan<byte> source, int cursor, Lookup lookup, bool escapeHtml, MissingCallback? onMissing, IBufferWriter<byte> writer)
    {
        if (TryConsumeCodeRegion(source, cursor, writer, out var afterCode))
        {
            return afterCode;
        }

        return TryExpandMacro(source, cursor, lookup, escapeHtml, onMissing, writer);
    }

    /// <summary>Detects a fenced or inline-code region at <paramref name="cursor"/>; when found, copies it through verbatim.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Current scan offset.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="afterCode">Offset just past the code region on success.</param>
    /// <returns>True when a code region was consumed.</returns>
    private static bool TryConsumeCodeRegion(ReadOnlySpan<byte> source, int cursor, IBufferWriter<byte> writer, out int afterCode)
    {
        afterCode = cursor;
        if (MarkdownCodeScanner.AtLineStart(source, cursor)
            && MarkdownCodeScanner.TryConsumeFence(source, cursor, out var fenceEnd))
        {
            Write(writer, source[cursor..fenceEnd]);
            afterCode = fenceEnd;
            return true;
        }

        if (cursor >= source.Length || source[cursor] is not (byte)'`')
        {
            return false;
        }

        var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, cursor);
        if (inlineEnd <= cursor)
        {
            return false;
        }

        Write(writer, source[cursor..inlineEnd]);
        afterCode = inlineEnd;
        return true;
    }

    /// <summary>Tries to expand a macro starting at <paramref name="cursor"/>; emits the substituted value or copies the literal byte through.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Current scan offset.</param>
    /// <param name="lookup">Lookup callback.</param>
    /// <param name="escapeHtml">Whether to HTML-escape resolved values.</param>
    /// <param name="onMissing">Missing-name callback.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Offset to resume from.</returns>
    private static int TryExpandMacro(ReadOnlySpan<byte> source, int cursor, Lookup lookup, bool escapeHtml, MissingCallback? onMissing, IBufferWriter<byte> writer)
    {
        if (cursor + Open.Length > source.Length || !source[cursor..].StartsWith(Open))
        {
            writer.GetSpan(1)[0] = source[cursor];
            writer.Advance(1);
            return cursor + 1;
        }

        var nameStart = cursor + Open.Length;
        var trimmedStart = SkipSpace(source, nameStart);
        var closeRel = source[trimmedStart..].IndexOf(Close);
        if (closeRel < 0)
        {
            writer.GetSpan(1)[0] = source[cursor];
            writer.Advance(1);
            return cursor + 1;
        }

        var trimmedEnd = TrimTrailingSpace(source, trimmedStart, trimmedStart + closeRel);
        var name = source[trimmedStart..trimmedEnd];
        if (!IsValidName(name))
        {
            writer.GetSpan(1)[0] = source[cursor];
            writer.Advance(1);
            return cursor + 1;
        }

        if (lookup(name, out var value))
        {
            EmitValue(value, escapeHtml, writer);
            return trimmedStart + closeRel + Close.Length;
        }

        onMissing?.Invoke(name);
        Write(writer, source[cursor..(trimmedStart + closeRel + Close.Length)]);
        return trimmedStart + closeRel + Close.Length;
    }

    /// <summary>Skips ASCII whitespace forward from <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset.</param>
    /// <returns>Offset of the first non-space byte.</returns>
    private static int SkipSpace(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && source[p] is (byte)' ' or (byte)'\t')
        {
            p++;
        }

        return p;
    }

    /// <summary>Trims trailing ASCII whitespace from a range.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>End offset trimmed of trailing whitespace.</returns>
    private static int TrimTrailingSpace(ReadOnlySpan<byte> source, int start, int end)
    {
        var p = end;
        while (p > start && source[p - 1] is (byte)' ' or (byte)'\t')
        {
            p--;
        }

        return p;
    }

    /// <summary>Returns true when <paramref name="name"/> is a non-empty identifier (letters / digits / dot / underscore / hyphen).</summary>
    /// <param name="name">Candidate bytes.</param>
    /// <returns>True for a valid macro name.</returns>
    private static bool IsValidName(ReadOnlySpan<byte> name)
    {
        if (name.Length is 0)
        {
            return false;
        }

        for (var i = 0; i < name.Length; i++)
        {
            if (!IsNameByte(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True for ASCII bytes valid in a macro name.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True when allowed.</returns>
    private static bool IsNameByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'.'
          or (byte)'_'
          or (byte)'-';

    /// <summary>Writes <paramref name="value"/> to <paramref name="writer"/>, optionally HTML-escaping.</summary>
    /// <param name="value">Resolved string value.</param>
    /// <param name="escapeHtml">Whether to escape the 5 entity characters.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitValue(string value, bool escapeHtml, IBufferWriter<byte> writer)
    {
        if (value.Length is 0)
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var rented = ArrayPool<byte>.Shared.Rent(max);
        try
        {
            var written = Encoding.UTF8.GetBytes(value, rented);
            if (escapeHtml)
            {
                EmitEscaped(rented.AsSpan(0, written), writer);
                return;
            }

            Write(writer, rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>HTML-escapes <paramref name="value"/> into <paramref name="writer"/>.</summary>
    /// <param name="value">UTF-8 source bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitEscaped(ReadOnlySpan<byte> value, IBufferWriter<byte> writer)
    {
        var p = 0;
        while (p < value.Length)
        {
            var rel = value[p..].IndexOfAny(EscapeChars);
            if (rel < 0)
            {
                Write(writer, value[p..]);
                return;
            }

            Write(writer, value[p..(p + rel)]);
            EmitEntity(value[p + rel], writer);
            p += rel + 1;
        }
    }

    /// <summary>Writes the named entity for a single escape byte.</summary>
    /// <param name="b">Byte to escape.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitEntity(byte b, IBufferWriter<byte> writer)
    {
        var entity = b switch
        {
            (byte)'&' => "&amp;"u8,
            (byte)'<' => "&lt;"u8,
            (byte)'>' => "&gt;"u8,
            (byte)'"' => "&quot;"u8,
            (byte)'\'' => "&#39;"u8,
            _ => default,
        };
        Write(writer, entity);
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
