// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Html;

namespace NuStreamDocs.Markdown;

/// <summary>Inline-link handler. Recognizes <c>[label](href "title")</c> and emits a matching <c>&lt;a&gt;</c> element with the href and title HTML-escaped.</summary>
internal static class LinkSpan
{
    /// <summary>Backslash byte that escapes the byte after it.</summary>
    private const byte Backslash = (byte)'\\';

    /// <summary>Open-bracket byte.</summary>
    private const byte OpenBracket = (byte)'[';

    /// <summary>Close-bracket byte.</summary>
    private const byte CloseBracket = (byte)']';

    /// <summary>Open-paren byte.</summary>
    private const byte OpenParen = (byte)'(';

    /// <summary>Close-paren byte.</summary>
    private const byte CloseParen = (byte)')';

    /// <summary>Double-quote byte.</summary>
    private const byte DoubleQuote = (byte)'"';

    /// <summary>Single-quote byte.</summary>
    private const byte SingleQuote = (byte)'\'';

    /// <summary>Less-than byte that opens an angle-bracket destination.</summary>
    private const byte AngleOpen = (byte)'<';

    /// <summary>Greater-than byte that closes an angle-bracket destination.</summary>
    private const byte AngleClose = (byte)'>';

    /// <summary>Bytes separating a link label from its destination.</summary>
    private const int LabelDestinationSeparatorLength = 2;

    /// <summary>Length of one backslash-escape sequence (the backslash and one escaped byte).</summary>
    private const int EscapeSequenceLength = 2;

    /// <summary>Handles an open bracket at <paramref name="pos"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the close paren on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="table">Render state of the whole source.</param>
    /// <returns>True when the link was complete and rendered.</returns>
    internal static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer,
        ref EmphasisTable table)
    {
        if (!TryReadShape(source, pos, out var shape))
        {
            return false;
        }

        // An inline link in the leading text of a link label stays literal, and the label of a
        // link renders its own inline links literally too, unless the link came from a reference
        // or sits inside emphasis.
        var nestable = HasNestableMarker(source, shape);
        if (table.LinksBlocked && !nestable)
        {
            return false;
        }

        table.LinksBlocked = false;
        InlineRenderer.FlushText(source, pendingTextStart, pos, writer);

        Utf8StringWriter.Write(writer, "<a href=\""u8);
        WriteDestination(source, shape, writer);
        Utf8StringWriter.Write(writer, "\""u8);
        WriteTitleAttribute(source, shape, writer);
        Utf8StringWriter.Write(writer, ">"u8);

        // Render the label as inline content so emphasis / code / etc.
        // still work inside link text.
        InlineRenderer.Render(source[shape.LabelStart..shape.LabelEnd], !(nestable || table.Depth > 0), writer);

        Utf8StringWriter.Write(writer, "</a>"u8);

        pos = shape.End;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Reads the <c>[label](href "title")</c> shape starting at <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Index of the opening bracket.</param>
    /// <param name="shape">Shape descriptor on success.</param>
    /// <returns>True when the shape is well-formed.</returns>
    internal static bool TryReadShape(ReadOnlySpan<byte> source, int start, out LinkShape shape)
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

        var contentStart = labelEnd + LabelDestinationSeparatorLength;
        var contentEnd = FindMatching(source, contentStart, OpenParen, CloseParen);
        if (contentEnd < 0)
        {
            return false;
        }

        shape = ParseDestination(source, start + 1, labelEnd, contentStart, contentEnd);
        return true;
    }

    /// <summary>Writes the destination as attribute text, dropping the backslash of every escaped ASCII punctuation byte.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="shape">Parsed link or image shape.</param>
    /// <param name="writer">UTF-8 sink.</param>
    internal static void WriteDestination(ReadOnlySpan<byte> source, in LinkShape shape, IBufferWriter<byte> writer)
    {
        var remaining = source[shape.HrefStart..shape.HrefEnd];
        while (!remaining.IsEmpty)
        {
            var backslash = remaining.IndexOf(Backslash);
            if (backslash < 0)
            {
                HtmlEscape.EscapeText(remaining, writer);
                return;
            }

            if (backslash + 1 >= remaining.Length || !InlineEscape.IsAsciiPunct(remaining[backslash + 1]))
            {
                HtmlEscape.EscapeText(remaining[..(backslash + 1)], writer);
                remaining = remaining[(backslash + 1)..];
                continue;
            }

            HtmlEscape.EscapeText(remaining[..backslash], writer);
            HtmlEscape.EscapeText(remaining.Slice(backslash + 1, 1), writer);
            remaining = remaining[(backslash + EscapeSequenceLength)..];
        }
    }

    /// <summary>Writes the <c> title="..."</c> attribute when the shape carries a title.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="shape">Parsed link or image shape.</param>
    /// <param name="writer">UTF-8 sink.</param>
    internal static void WriteTitleAttribute(ReadOnlySpan<byte> source, in LinkShape shape, IBufferWriter<byte> writer)
    {
        if (shape.TitleStart < 0)
        {
            return;
        }

        Utf8StringWriter.Write(writer, " title=\""u8);
        HtmlEscape.EscapeText(source[shape.TitleStart..shape.TitleEnd], writer);
        Utf8StringWriter.Write(writer, "\""u8);
    }

    /// <summary>Finds the matching close byte, respecting nested open/close pairs and backslash-escaped bytes.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="searchFrom">First byte to consider.</param>
    /// <param name="open">Open marker.</param>
    /// <param name="close">Close marker.</param>
    /// <returns>Index of the matching close, or -1.</returns>
    internal static int FindMatching(ReadOnlySpan<byte> source, int searchFrom, byte open, byte close)
    {
        var depth = 1;
        for (var i = searchFrom; i < source.Length; i++)
        {
            var b = source[i];
            if (b == Backslash)
            {
                i++;
                continue;
            }

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

    /// <summary>True when whitespace separates the open paren from the destination, the form a resolved reference link takes when it may nest with other links.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="shape">Parsed link shape.</param>
    /// <returns>True when the link may nest with other links.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasNestableMarker(ReadOnlySpan<byte> source, in LinkShape shape) =>
        AsciiByteHelpers.IsAsciiWhitespace(source[shape.LabelEnd + LabelDestinationSeparatorLength]);

    /// <summary>Splits the text between the parentheses into a destination and an optional quoted title.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="labelStart">Inclusive start of the label.</param>
    /// <param name="labelEnd">Exclusive end of the label.</param>
    /// <param name="contentStart">Index just after the opening parenthesis.</param>
    /// <param name="contentEnd">Index of the closing parenthesis.</param>
    /// <returns>The link shape.</returns>
    private static LinkShape ParseDestination(
        ReadOnlySpan<byte> source,
        int labelStart,
        int labelEnd,
        int contentStart,
        int contentEnd)
    {
        var start = contentStart;
        while (start < contentEnd && AsciiByteHelpers.IsAsciiWhitespace(source[start]))
        {
            start++;
        }

        var end = TrimEndIndex(source, start, contentEnd);
        var open = end > start ? FindTitleOpen(source, start, end - 1) : -1;
        var titleStart = open < 0 ? -1 : open + 1;
        var titleEnd = open < 0 ? -1 : end - 1;
        var destinationEnd = open < 0 ? end : TrimEndIndex(source, start, open);

        return HasAngleBrackets(source, start, destinationEnd)
            ? new(labelStart, labelEnd, start + 1, destinationEnd - 1, contentEnd + 1, titleStart, titleEnd)
            : new(labelStart, labelEnd, start, destinationEnd, contentEnd + 1, titleStart, titleEnd);
    }

    /// <summary>Returns the index just past the last non-whitespace byte in [<paramref name="start"/>, <paramref name="end"/>).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Inclusive lower bound.</param>
    /// <param name="end">Exclusive upper bound.</param>
    /// <returns>The trimmed exclusive end.</returns>
    private static int TrimEndIndex(ReadOnlySpan<byte> source, int start, int end)
    {
        var i = end;
        while (i > start && AsciiByteHelpers.IsAsciiWhitespace(source[i - 1]))
        {
            i--;
        }

        return i;
    }

    /// <summary>Finds the quote that opens a trailing title: a quote byte preceded by whitespace that is not the first byte of the destination.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Inclusive start of the trimmed text.</param>
    /// <param name="closeQuote">Index of the last byte, which must be the closing quote.</param>
    /// <returns>Index of the opening quote, or -1.</returns>
    private static int FindTitleOpen(ReadOnlySpan<byte> source, int start, int closeQuote)
    {
        var quote = source[closeQuote];
        if (quote is not (DoubleQuote or SingleQuote))
        {
            return -1;
        }

        for (var i = closeQuote - 1; i > start; i--)
        {
            if (source[i] == quote && AsciiByteHelpers.IsAsciiWhitespace(source[i - 1]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>True when [<paramref name="start"/>, <paramref name="end"/>) is a non-empty <c>&lt;destination&gt;</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>True when the range starts with <c>&lt;</c> and ends with <c>&gt;</c>.</returns>
    private static bool HasAngleBrackets(ReadOnlySpan<byte> source, int start, int end) =>
        end - start >= LabelDestinationSeparatorLength && source[start] == AngleOpen && source[end - 1] == AngleClose;

    /// <summary>Offsets that describe a parsed inline link.</summary>
    /// <param name="LabelStart">Inclusive start of the label content.</param>
    /// <param name="LabelEnd">Exclusive end of the label content (the close bracket).</param>
    /// <param name="HrefStart">Inclusive start of the href.</param>
    /// <param name="HrefEnd">Exclusive end of the href.</param>
    /// <param name="End">Index after the close paren.</param>
    /// <param name="TitleStart">Inclusive start of the title content, or -1 when the link has no title.</param>
    /// <param name="TitleEnd">Exclusive end of the title content, or -1 when the link has no title.</param>
    internal readonly record struct LinkShape(
        int LabelStart,
        int LabelEnd,
        int HrefStart,
        int HrefEnd,
        int End,
        int TitleStart,
        int TitleEnd);
}
