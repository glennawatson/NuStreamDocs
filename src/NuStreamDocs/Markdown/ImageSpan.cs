// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Html;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Inline-image handler. Recognizes <c>![alt](url)</c> at the cursor and emits a matching
/// <c>&lt;img src="url" alt="alt"&gt;</c> element with both attribute values HTML-escaped.
/// </summary>
internal static class ImageSpan
{
    /// <summary>Open-bracket byte that must follow the leading <c>!</c>.</summary>
    private const byte OpenBracket = (byte)'[';

    /// <summary>Backtick byte that delimits a code span inside the alt text.</summary>
    private const byte Backtick = (byte)'`';

    /// <summary>Backslash byte that escapes the byte after it.</summary>
    private const byte Backslash = (byte)'\\';

    /// <summary>Bang byte that turns a following link into an image.</summary>
    private const byte Bang = (byte)'!';

    /// <summary>Length of one backslash-escape sequence (the backslash and one escaped byte).</summary>
    private const int EscapeLength = 2;

    /// <summary>Handles an image span at <paramref name="pos"/> when the next byte is <c>[</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor on the leading <c>!</c>; advanced past the close paren on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when the cursor advanced past a complete <c>![…](…)</c> shape.</returns>
    internal static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        if (pos + 1 >= source.Length || source[pos + 1] != OpenBracket)
        {
            return false;
        }

        if (!LinkSpan.TryReadShape(source, pos + 1, out var shape))
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, pos, writer);

        Utf8StringWriter.Write(writer, "<img alt=\""u8);
        WriteAltText(source[shape.LabelStart..shape.LabelEnd], true, writer);
        Utf8StringWriter.Write(writer, "\" src=\""u8);
        LinkSpan.WriteDestination(source, shape, writer);
        Utf8StringWriter.Write(writer, "\""u8);
        LinkSpan.WriteTitleAttribute(source, shape, writer);
        Utf8StringWriter.Write(writer, " />"u8);

        pos = shape.End;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Writes the alt attribute value: code spans and inline links are reduced to their text and everything else is escaped as written.</summary>
    /// <param name="label">Image label bytes.</param>
    /// <param name="reduceLinks">True when inline links in <paramref name="label"/> are reduced to their label text; false when they stay as written.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteAltText(ReadOnlySpan<byte> label, bool reduceLinks, IBufferWriter<byte> writer)
    {
        var i = label.IndexOfAny(Backtick, OpenBracket, Backslash);
        if (i < 0)
        {
            HtmlEscape.EscapeText(label, writer);
            return;
        }

        var textStart = 0;
        while (i < label.Length)
        {
            var next = label[i] switch
            {
                Backslash => i + EscapeLength,
                OpenBracket => reduceLinks ? WriteAltLink(label, i, ref textStart, writer) : i + 1,
                _ => WriteAltCodeSpan(label, i, ref textStart, writer)
            };

            var rel = next < label.Length ? label[next..].IndexOfAny(Backtick, OpenBracket, Backslash) : -1;
            i = rel < 0 ? label.Length : next + rel;
        }

        HtmlEscape.EscapeText(label[textStart..], writer);
    }

    /// <summary>Writes the label of the inline link that starts at <paramref name="i"/>, when there is one.</summary>
    /// <param name="label">Image label bytes.</param>
    /// <param name="i">Index of the open bracket.</param>
    /// <param name="textStart">Start of the label text not yet written; advanced past the link when one was written.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int WriteAltLink(ReadOnlySpan<byte> label, int i, ref int textStart, IBufferWriter<byte> writer)
    {
        if ((i > 0 && label[i - 1] is Bang) || !LinkSpan.TryReadShape(label, i, out var shape))
        {
            return i + 1;
        }

        HtmlEscape.EscapeText(label[textStart..i], writer);
        WriteAltText(label[shape.LabelStart..shape.LabelEnd], false, writer);
        textStart = shape.End;
        return shape.End;
    }

    /// <summary>Writes the content of the code span that starts at <paramref name="i"/>, when there is one.</summary>
    /// <param name="label">Image label bytes.</param>
    /// <param name="i">Index of the first backtick.</param>
    /// <param name="textStart">Start of the label text not yet written; advanced past the code span when one was written.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int WriteAltCodeSpan(ReadOnlySpan<byte> label, int i, ref int textStart, IBufferWriter<byte> writer)
    {
        var runLength = AsciiByteHelpers.RunLength(label, i, Backtick);
        var contentStart = i + runLength;
        var closeStart = CodeSpan.FindMatchingClose(label, contentStart, runLength);
        if (closeStart < 0)
        {
            return contentStart;
        }

        HtmlEscape.EscapeText(label[textStart..i], writer);
        HtmlEscape.EscapeText(AsciiByteHelpers.TrimAsciiWhitespace(label[contentStart..closeStart]), writer);
        textStart = closeStart + runLength;
        return textStart;
    }
}
