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
        WriteAltText(source[shape.LabelStart..shape.LabelEnd], writer);
        Utf8StringWriter.Write(writer, "\" src=\""u8);
        LinkSpan.WriteDestination(source, shape, writer);
        Utf8StringWriter.Write(writer, "\""u8);
        LinkSpan.WriteTitleAttribute(source, shape, writer);
        Utf8StringWriter.Write(writer, " />"u8);

        pos = shape.End;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Writes the alt attribute value: code-span backticks are dropped and their content kept; everything else is escaped as written.</summary>
    /// <param name="label">Image label bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteAltText(ReadOnlySpan<byte> label, IBufferWriter<byte> writer)
    {
        var firstBacktick = label.IndexOf(Backtick);
        if (firstBacktick < 0)
        {
            HtmlEscape.EscapeText(label, writer);
            return;
        }

        var textStart = 0;
        var i = firstBacktick;
        while (i < label.Length)
        {
            if (label[i] != Backtick)
            {
                i++;
                continue;
            }

            var runLength = AsciiByteHelpers.RunLength(label, i, Backtick);
            var contentStart = i + runLength;
            var closeStart = CodeSpan.FindMatchingClose(label, contentStart, runLength);
            if (closeStart < 0)
            {
                i = contentStart;
                continue;
            }

            HtmlEscape.EscapeText(label[textStart..i], writer);
            HtmlEscape.EscapeText(AsciiByteHelpers.TrimAsciiWhitespace(label[contentStart..closeStart]), writer);
            i = closeStart + runLength;
            textStart = i;
        }

        HtmlEscape.EscapeText(label[textStart..], writer);
    }
}
