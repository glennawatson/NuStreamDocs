// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.InlineHilite;

/// <summary>
/// Stateless UTF-8 inline-hilite rewriter. Walks the source byte
/// stream, locating inline-code spans that begin with the
/// <c>#!lang</c> shebang and rewriting them into a
/// language-classed <c>code</c> element. Other inline-code spans
/// and fenced-code regions are passed through verbatim.
/// </summary>
internal static class InlineHiliteRewriter
{
    /// <summary>Width of the <c>#!</c> shebang prefix.</summary>
    private const int ShebangLength = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i) && MarkdownCodeScanner.TryConsumeFence(source, i, out var fenceEnd))
            {
                writer.Write(source[i..fenceEnd]);
                i = fenceEnd;
                continue;
            }

            if (source[i] is (byte)'`' && TryRewriteShebangCode(source, i, writer, out var consumed))
            {
                i += consumed;
                continue;
            }

            if (source[i] is (byte)'`')
            {
                var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, i);
                writer.Write(source[i..inlineEnd]);
                i = inlineEnd;
                continue;
            }

            var dest = writer.GetSpan(1);
            dest[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Tries to match a <c>`#!lang code`</c> span starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor on the leading backtick.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a shebang span was rewritten.</returns>
    private static bool TryRewriteShebangCode(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        var run = CountBackticks(source, offset);
        var contentStart = offset + run;
        if (contentStart + ShebangLength >= source.Length
            || source[contentStart] is not (byte)'#'
            || source[contentStart + 1] is not (byte)'!')
        {
            return false;
        }

        var langStart = contentStart + ShebangLength;
        var langEnd = ScanLanguage(source, langStart);
        if (langEnd <= langStart || langEnd >= source.Length || source[langEnd] is not (byte)' ')
        {
            return false;
        }

        var codeStart = langEnd + 1;
        var closeOffset = FindCloseRun(source, codeStart, run);
        if (closeOffset < 0)
        {
            return false;
        }

        EmitInlineHighlight(source[langStart..langEnd], source[codeStart..closeOffset], writer);
        consumed = closeOffset + run - offset;
        return true;
    }

    /// <summary>Counts the backtick run starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the first backtick.</param>
    /// <returns>Number of backticks.</returns>
    private static int CountBackticks(ReadOnlySpan<byte> source, int offset)
    {
        var run = 0;
        while (offset + run < source.Length && source[offset + run] is (byte)'`')
        {
            run++;
        }

        return run;
    }

    /// <summary>Scans an ASCII language token (alphanumeric / hyphen / plus / dot / underscore).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position just past the <c>#!</c> shebang.</param>
    /// <returns>Exclusive end of the language token.</returns>
    private static int ScanLanguage(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && IsLanguageByte(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Returns true when <paramref name="b"/> is a valid language-class byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for letters, digits, <c>-</c>, <c>+</c>, <c>.</c>, <c>_</c>.</returns>
    private static bool IsLanguageByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'-' or (byte)'+' or (byte)'.' or (byte)'_';

    /// <summary>Finds a backtick run of exactly <paramref name="width"/> bytes starting at or after <paramref name="from"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="from">Search-start offset.</param>
    /// <param name="width">Required run width.</param>
    /// <returns>Offset of the closing run on success; -1 otherwise.</returns>
    private static int FindCloseRun(ReadOnlySpan<byte> source, int from, int width)
    {
        var p = from;
        while (p < source.Length)
        {
            if (source[p] is not (byte)'`')
            {
                p++;
                continue;
            }

            var closeRun = CountBackticks(source, p);
            if (closeRun == width)
            {
                return p;
            }

            p += closeRun;
        }

        return -1;
    }

    /// <summary>Emits the rewritten inline-highlight element.</summary>
    /// <param name="language">UTF-8 language token.</param>
    /// <param name="code">UTF-8 code body.</param>
    /// <param name="writer">Sink.</param>
    private static void EmitInlineHighlight(ReadOnlySpan<byte> language, ReadOnlySpan<byte> code, IBufferWriter<byte> writer)
    {
        writer.Write("<code class=\"highlight language-"u8);
        writer.Write(language);
        writer.Write("\">"u8);
        WriteHtmlEscaped(writer, code);
        writer.Write("</code>"u8);
    }

    /// <summary>Writes <paramref name="bytes"/> with the four HTML-special bytes (<c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, <c>"</c>) expanded to their entities.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="bytes">UTF-8 bytes to escape.</param>
    private static void WriteHtmlEscaped(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        var runStart = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var entity = EntityFor(bytes[i]);
            if (entity.IsEmpty)
            {
                continue;
            }

            if (i > runStart)
            {
                writer.Write(bytes[runStart..i]);
            }

            writer.Write(entity);
            runStart = i + 1;
        }

        if (runStart >= bytes.Length)
        {
            return;
        }

        writer.Write(bytes[runStart..]);
    }

    /// <summary>Returns the entity bytes for <paramref name="b"/> or an empty span when the byte is plain.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>Entity bytes or empty.</returns>
    private static ReadOnlySpan<byte> EntityFor(byte b) => b switch
    {
        (byte)'<' => "&lt;"u8,
        (byte)'>' => "&gt;"u8,
        (byte)'&' => "&amp;"u8,
        (byte)'"' => "&quot;"u8,
        _ => default,
    };
}
