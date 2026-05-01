// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Html;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Inline-link handler. Recognises <c>[label](href)</c> and emits a
/// matching <c>&lt;a&gt;</c> element with the href HTML-escaped.
/// </summary>
internal static class LinkSpan
{
    /// <summary>Open-bracket byte.</summary>
    private const byte OpenBracket = (byte)'[';

    /// <summary>Close-bracket byte.</summary>
    private const byte CloseBracket = (byte)']';

    /// <summary>Open-paren byte.</summary>
    private const byte OpenParen = (byte)'(';

    /// <summary>Close-paren byte.</summary>
    private const byte CloseParen = (byte)')';

    /// <summary>
    /// Handles an open bracket at <paramref name="pos"/>.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the close paren on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when the link was complete and rendered.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        if (!TryReadShape(source, pos, out var shape))
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, pos, writer);

        Write("<a href=\""u8, writer);
        HtmlEscape.EscapeText(source[shape.HrefStart..shape.HrefEnd], writer);
        Write("\">"u8, writer);

        // Render the label as inline content so emphasis / code / etc.
        // still work inside link text.
        InlineRenderer.Render(source[shape.LabelStart..shape.LabelEnd], writer);

        Write("</a>"u8, writer);

        pos = shape.End;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Reads the <c>[label](href)</c> shape starting at <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Index of the opening bracket.</param>
    /// <param name="shape">Shape descriptor on success.</param>
    /// <returns>True when the shape is well-formed.</returns>
    public static bool TryReadShape(ReadOnlySpan<byte> source, int start, out LinkShape shape)
    {
        shape = default;
        if (source[start] != OpenBracket)
        {
            return false;
        }

        var labelEnd = FindMatching(source, start + 1, OpenBracket, CloseBracket);
        if (labelEnd < 0 || labelEnd + 1 >= source.Length || source[labelEnd + 1] != OpenParen)
        {
            return false;
        }

        var hrefStart = labelEnd + 2;
        var hrefEnd = FindMatching(source, hrefStart, OpenParen, CloseParen);
        if (hrefEnd < 0)
        {
            return false;
        }

        shape = new(start + 1, labelEnd, hrefStart, hrefEnd, hrefEnd + 1);
        return true;
    }

    /// <summary>Finds the matching close byte, respecting nested open/close pairs.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="searchFrom">First byte to consider.</param>
    /// <param name="open">Open marker.</param>
    /// <param name="close">Close marker.</param>
    /// <returns>Index of the matching close, or -1.</returns>
    public static int FindMatching(ReadOnlySpan<byte> source, int searchFrom, byte open, byte close)
    {
        var depth = 1;
        for (var i = searchFrom; i < source.Length; i++)
        {
            var b = source[i];
            if (b == open)
            {
                depth++;
                continue;
            }

            if (b != close)
            {
                continue;
            }

            depth--;
            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void Write(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Offsets that describe a parsed inline link.</summary>
    /// <param name="LabelStart">Inclusive start of the label content.</param>
    /// <param name="LabelEnd">Exclusive end of the label content (the close bracket).</param>
    /// <param name="HrefStart">Inclusive start of the href.</param>
    /// <param name="HrefEnd">Exclusive end of the href (the close paren).</param>
    /// <param name="End">Index after the close paren.</param>
    public readonly record struct LinkShape(
        int LabelStart,
        int LabelEnd,
        int HrefStart,
        int HrefEnd,
        int End);
}
