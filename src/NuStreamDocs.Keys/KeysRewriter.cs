// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Keys;

/// <summary>
/// Stateless UTF-8 keys rewriter. Walks the source byte stream and
/// replaces <c>++key1+key2+…++</c> spans with the keys-span markup
/// pymdownx.keys produces. Fenced code and inline code are skipped
/// verbatim.
/// </summary>
internal static class KeysRewriter
{
    /// <summary>Width of the <c>++</c> opening/closing marker.</summary>
    private const int MarkerLength = 2;

    /// <summary>OR-mask that maps an ASCII uppercase letter to its lowercase form (<c>'A'</c> <c>0x41</c> | <c>0x20</c> = <c>'a'</c> <c>0x61</c>).</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        CodeAwareRewriter.Run(source, writer, TryRewriteKeys);

    /// <summary>Tries to match a <c>++…++</c> shortcut starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a substitution was emitted.</returns>
    private static bool TryRewriteKeys(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (offset + MarkerLength >= source.Length
            || source[offset] is not (byte)'+'
            || source[offset + 1] is not (byte)'+')
        {
            return false;
        }

        var contentStart = offset + MarkerLength;
        if (!TryFindClose(source, contentStart, out var contentEnd))
        {
            return false;
        }

        var content = source[contentStart..contentEnd];
        if (content.IsEmpty)
        {
            return false;
        }

        EmitKeysSpan(content, writer);
        consumed = contentEnd + MarkerLength - offset;
        return true;
    }

    /// <summary>Splits <paramref name="content"/> on <c>+</c> separators and writes the structured keys span.</summary>
    /// <param name="content">UTF-8 bytes between the opening and closing <c>++</c>.</param>
    /// <param name="writer">Sink.</param>
    private static void EmitKeysSpan(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
    {
        writer.Write("<span class=\"keys\">"u8);

        var first = true;
        var cursor = 0;
        while (cursor < content.Length)
        {
            var rel = content[cursor..].IndexOf((byte)'+');
            var tokenEnd = rel < 0 ? content.Length : cursor + rel;
            var token = content[cursor..tokenEnd];

            if (!first)
            {
                writer.Write("<span>+</span>"u8);
            }

            EmitKbd(token, writer);
            first = false;

            if (rel < 0)
            {
                break;
            }

            cursor = tokenEnd + 1;
        }

        writer.Write("</span>"u8);
    }

    /// <summary>Writes a single <c>&lt;kbd class="key-…"&gt;label&lt;/kbd&gt;</c> for <paramref name="token"/>.</summary>
    /// <param name="token">UTF-8 token bytes (no <c>+</c>).</param>
    /// <param name="writer">Sink.</param>
    private static void EmitKbd(ReadOnlySpan<byte> token, IBufferWriter<byte> writer)
    {
        // Pure ASCII fast path: lowercase into a stack buffer, no allocation, then probe the byte-keyed lookup.
        const int LookupStackLimit = 128;
        var lookupBuf = token.Length <= LookupStackLimit ? stackalloc byte[token.Length] : new byte[token.Length];
        for (var i = 0; i < token.Length; i++)
        {
            var b = token[i];
            lookupBuf[i] = b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b | AsciiCaseBit) : b;
        }

        var lookup = (ReadOnlySpan<byte>)lookupBuf;
        if (KeyNames.TryGet(lookup, out var entry))
        {
            writer.Write("<kbd class=\"key-"u8);
            writer.Write(entry.ClassSuffix);
            writer.Write("\">"u8);
            writer.Write(entry.Label);
            writer.Write("</kbd>"u8);
            return;
        }

        // Unknown token: derive a sanitized class from the lower-cased lookup bytes and emit the literal label.
        writer.Write("<kbd class=\"key-"u8);
        WriteSanitizedClass(writer, lookup);
        writer.Write("\">"u8);
        writer.Write(token);
        writer.Write("</kbd>"u8);
    }

    /// <summary>Writes <paramref name="text"/> as UTF-8, replacing non-class-safe ASCII bytes with <c>-</c> and discarding non-ASCII bytes.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="text">Lower-cased UTF-8 token bytes.</param>
    private static void WriteSanitizedClass(IBufferWriter<byte> writer, ReadOnlySpan<byte> text)
    {
        var dst = writer.GetSpan(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var b = text[i];
            dst[i] = b is >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9' or (byte)'-' ? b : (byte)'-';
        }

        writer.Advance(text.Length);
    }

    /// <summary>Searches for the matching <c>++</c> on the same line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Offset just past the opening <c>++</c>.</param>
    /// <param name="contentEnd">Offset of the first byte of the closing marker on success.</param>
    /// <returns>True when a closer was found.</returns>
    private static bool TryFindClose(ReadOnlySpan<byte> source, int start, out int contentEnd)
    {
        contentEnd = 0;
        if (start >= source.Length || source[start] is (byte)' ' or (byte)'+' or (byte)'\n')
        {
            return false;
        }

        for (var p = start; p + 1 < source.Length; p++)
        {
            if (source[p] is (byte)'\n')
            {
                return false;
            }

            if (source[p] is not (byte)'+' || source[p + 1] is not (byte)'+')
            {
                continue;
            }

            contentEnd = p;
            return true;
        }

        return false;
    }
}
