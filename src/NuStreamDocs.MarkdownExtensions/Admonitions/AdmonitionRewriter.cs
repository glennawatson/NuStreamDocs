// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.Admonitions;

/// <summary>Rewrites <c>!!! type "title"</c> blocks and optional GitHub <c>&gt; [!TYPE]</c> alerts into <c>&lt;div class="admonition type"&gt;</c> HTML.</summary>
internal static class AdmonitionRewriter
{
    /// <summary>Gets the opener marker.</summary>
    private static ReadOnlySpan<byte> Opener => "!!! "u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/> with GitHub alerts enabled.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        Rewrite(source, true, writer);

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="gitHubAlerts">Whether to convert GitHub <c>&gt; [!TYPE]</c> blockquotes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    internal static void Rewrite(ReadOnlySpan<byte> source, bool gitHubAlerts, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (TryRewriteBlockAt(source, i, gitHubAlerts, writer, out var blockEnd))
            {
                i = blockEnd;
                continue;
            }

            var lineEnd = Utf8LineSpan.LfLineEnd(source, i);
            writer.Write(source[i..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Tries to rewrite or skip a fenced code block, <c>!!!</c> admonition, or GitHub alert at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <param name="gitHubAlerts">Whether GitHub alerts are enabled.</param>
    /// <param name="writer">Output sink.</param>
    /// <param name="blockEnd">Exclusive end offset of the handled block.</param>
    /// <returns><c>true</c> when a block was consumed.</returns>
    private static bool TryRewriteBlockAt(
        ReadOnlySpan<byte> source,
        int offset,
        bool gitHubAlerts,
        IBufferWriter<byte> writer,
        out int blockEnd)
    {
        if (MarkdownCodeScanner.TryConsumeFence(source, offset, out blockEnd))
        {
            writer.Write(source[offset..blockEnd]);
            return true;
        }

        if (TryParseOpener(source, offset, out var typeStart, out var typeLen, out var titleStart, out var titleLen, out var headerEnd))
        {
            blockEnd = IndentedBlockScanner.ConsumeBody(source, headerEnd);
            EmitAdmonition(
                source.Slice(typeStart, typeLen),
                titleLen is 0 ? default : source.Slice(titleStart, titleLen),
                source[headerEnd..blockEnd],
                false,
                writer);
            return true;
        }

        if (gitHubAlerts
            && GitHubAlertScanner.TryParseAlert(source, offset, out var alertType, out var alertTitle, out var alertHeaderEnd, out blockEnd))
        {
            EmitAdmonition(alertType, alertTitle, source[alertHeaderEnd..blockEnd], true, writer);
            return true;
        }

        blockEnd = 0;
        return false;
    }

    /// <summary>Tries to parse an admonition opener starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="offset">Candidate line-start offset.</param>
    /// <param name="typeStart">Set to the offset of the type token on success.</param>
    /// <param name="typeLen">Set to the length of the type token on success.</param>
    /// <param name="titleStart">Set to the offset of the title token on success (zero when absent).</param>
    /// <param name="titleLen">Set to the length of the title token on success (zero when absent).</param>
    /// <param name="headerEnd">Set to the byte offset just past the opener line's terminator.</param>
    /// <returns>True when <paramref name="offset"/> begins a valid opener.</returns>
    private static bool TryParseOpener(
        ReadOnlySpan<byte> source,
        int offset,
        out int typeStart,
        out int typeLen,
        out int titleStart,
        out int titleLen,
        out int headerEnd)
    {
        typeStart = 0;
        typeLen = 0;
        titleStart = 0;
        titleLen = 0;
        headerEnd = 0;

        return offset + Opener.Length <= source.Length && source[offset..].StartsWith(Opener)
                                                       && OpenerLineParser.TryParseTypeAndTitle(
                                                           source,
                                                           offset + Opener.Length,
                                                           out typeStart,
                                                           out typeLen,
                                                           out titleStart,
                                                           out titleLen,
                                                           out headerEnd);
    }

    /// <summary>Emits one admonition <c>&lt;div&gt;</c> block.</summary>
    /// <param name="type">UTF-8 type token.</param>
    /// <param name="title">UTF-8 title bytes; empty span when no title was specified.</param>
    /// <param name="body">UTF-8 body bytes.</param>
    /// <param name="isBlockquoteBody"><c>true</c> to strip <c>&gt;</c> prefixes; <c>false</c> to de-indent 4-space lines.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitAdmonition(
        ReadOnlySpan<byte> type,
        ReadOnlySpan<byte> title,
        ReadOnlySpan<byte> body,
        bool isBlockquoteBody,
        IBufferWriter<byte> writer)
    {
        writer.Write("\n<div class=\"admonition "u8);
        writer.Write(type);
        writer.Write("\">\n<p class=\"admonition-title\">"u8);
        if (title.Length is 0)
        {
            OpenerLineParser.WriteTitleCase(type, writer);
        }
        else
        {
            XmlEntityEscaper.WriteEscaped(writer, title, XmlEntityEscaper.Mode.HtmlAttribute);
        }

        writer.Write("</p>\n\n"u8);
        if (isBlockquoteBody)
        {
            GitHubAlertScanner.WriteStrippedQuoteBody(body, writer);
        }
        else
        {
            IndentedBlockScanner.WriteDeindented(body, writer);
        }

        writer.Write("\n</div>\n\n"u8);
    }
}
