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

    /// <summary>Handles an image span at <paramref name="pos"/> when the next byte is <c>[</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor on the leading <c>!</c>; advanced past the close paren on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when the cursor advanced past a complete <c>![…](…)</c> shape.</returns>
    public static bool TryHandle(
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

        Utf8StringWriter.Write(writer, "<img src=\""u8);
        HtmlEscape.EscapeText(source[shape.HrefStart..shape.HrefEnd], writer);
        Utf8StringWriter.Write(writer, "\" alt=\""u8);
        HtmlEscape.EscapeText(source[shape.LabelStart..shape.LabelEnd], writer);
        Utf8StringWriter.Write(writer, "\">"u8);

        pos = shape.End;
        pendingTextStart = pos;
        return true;
    }
}
