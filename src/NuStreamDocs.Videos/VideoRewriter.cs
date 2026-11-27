// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Videos;

/// <summary>Rewrites Markdown video URLs and <c>![title](url)</c> media links into responsive HTML blocks while preserving code and raw HTML.</summary>
internal static class VideoRewriter
{
    /// <summary>Byte length of a two-byte backslash escape sequence.</summary>
    private const int EscapeSequenceLength = 2;

    /// <summary>Offset two bytes before the current position when checking CRLF/LF blank-line history.</summary>
    private const int TwoBytesBack = 2;

    /// <summary>Offset three bytes before the current position when checking CRLF blank-line history.</summary>
    private const int ThreeBytesBack = 3;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/> using <paramref name="options"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    /// <param name="options">Configured video options.</param>
    /// <param name="resolvedTwitchParent">Pre-resolved Twitch parent host.</param>
    internal static void Rewrite(
        ReadOnlySpan<byte> source,
        IBufferWriter<byte> writer,
        in VideoOptions options,
        ReadOnlySpan<byte> resolvedTwitchParent)
    {
        if (source.IsEmpty)
        {
            return;
        }

        if (options.Providers is VideoProviders.None
            || (!options.EmbedBareUrls && !options.EmbedImageLinks && !options.EmbedStandaloneLinks))
        {
            Utf8StringWriter.Write(writer, source);
            return;
        }

        var session = new RewriteSession(source, writer, options, resolvedTwitchParent);
        var pos = 0;
        var prevBlank = true;
        while (pos < source.Length)
        {
            var lineEnd = Utf8LineSpan.FindLineEnd(source, pos);
            var nextPos = Utf8LineSpan.AdvancePastLineTerminator(source, lineEnd);
            var line = source[pos..lineEnd];

            if (TrySkipBlockConstruct(source, pos, line, prevBlank, out var skipTo, out var nextPrevBlank))
            {
                pos = skipTo;
                prevBlank = nextPrevBlank;
                continue;
            }

            if (AsciiByteHelpers.IsAllAsciiWhitespace(line))
            {
                prevBlank = true;
                pos = nextPos;
                continue;
            }

            prevBlank = ProcessContentLine(ref session, pos, lineEnd, nextPos, line);
            pos = nextPos;
        }

        if (session.LastEmit >= source.Length)
        {
            return;
        }

        Utf8StringWriter.Write(writer, source[session.LastEmit..]);
    }

    /// <summary>Processes a non-blank Markdown content line and reports whether it ended in a blank-line separator.</summary>
    /// <param name="session">Active rewrite session.</param>
    /// <param name="lineStart">Start offset of the line in <see cref="RewriteSession.Source"/>.</param>
    /// <param name="lineEnd">End offset of the line (before CR/LF).</param>
    /// <param name="nextLineStart">Start offset of the next line.</param>
    /// <param name="line">Line slice without terminator.</param>
    /// <returns><c>true</c> when the entire line was replaced with a standalone block followed by a blank line.</returns>
    private static bool ProcessContentLine(
        ref RewriteSession session,
        int lineStart,
        int lineEnd,
        int nextLineStart,
        ReadOnlySpan<byte> line)
    {
        if (line.IndexOf("://"u8) < 0)
        {
            return false;
        }

        if (TryRewriteStandaloneLine(ref session, lineStart, nextLineStart, line))
        {
            return true;
        }

        if (session.Options.EmbedImageLinks && line.IndexOf("!["u8) >= 0)
        {
            RewriteInlineMediaImages(ref session, lineStart, lineEnd);
        }

        return false;
    }

    /// <summary>Skips fenced code blocks, indented code blocks, HTML comments, and raw Type-1 HTML blocks.</summary>
    /// <param name="source">Full UTF-8 markdown buffer.</param>
    /// <param name="lineStart">Start offset of the current line.</param>
    /// <param name="line">Current line slice without terminator.</param>
    /// <param name="prevBlank">Whether the preceding line was blank (required to open an indented code block).</param>
    /// <param name="skipTo">Next byte offset to resume scanning from when skipped.</param>
    /// <param name="nextPrevBlank">Updated blank-line state after the skipped construct.</param>
    /// <returns><c>true</c> when a code or raw HTML construct was skipped.</returns>
    private static bool TrySkipBlockConstruct(
        ReadOnlySpan<byte> source,
        int lineStart,
        ReadOnlySpan<byte> line,
        bool prevBlank,
        out int skipTo,
        out bool nextPrevBlank)
    {
        skipTo = 0;
        nextPrevBlank = false;

        var indent = VideoMarkdownScanner.CountLeadingSpaces(line, out var hasLeadingTab);
        if ((hasLeadingTab || indent >= VideoMarkdownScanner.IndentedCodeColumn) && prevBlank)
        {
            skipTo = VideoMarkdownScanner.ConsumeIndentedCodeRun(source, lineStart);
            nextPrevBlank = true;
            return true;
        }

        if (indent >= VideoMarkdownScanner.IndentedCodeColumn)
        {
            return false;
        }

        var trimmedStart = lineStart + indent;
        if (MarkdownCodeScanner.TryConsumeFence(source, trimmedStart, out var fenceEnd))
        {
            skipTo = fenceEnd;
            return true;
        }

        var body = line[indent..];
        if (body.StartsWith("<!--"u8))
        {
            skipTo = VideoMarkdownScanner.ConsumeUntilToken(source, trimmedStart + "<!--"u8.Length, "-->"u8);
            return true;
        }

        return TrySkipRawHtmlTagBlock(source, trimmedStart, body, out skipTo);
    }

    /// <summary>Skips raw HTML tag blocks while keeping <c>&lt;https://...&gt;</c> autolinks eligible for video embedding.</summary>
    /// <param name="source">Full UTF-8 markdown buffer.</param>
    /// <param name="trimmedStart">Offset of the leading <c>&lt;</c> byte.</param>
    /// <param name="body">Indent-trimmed line slice.</param>
    /// <param name="skipTo">Next byte offset after the skipped HTML line or block.</param>
    /// <returns><c>true</c> when the line opened a raw HTML tag and was skipped.</returns>
    private static bool TrySkipRawHtmlTagBlock(
        ReadOnlySpan<byte> source,
        int trimmedStart,
        ReadOnlySpan<byte> body,
        out int skipTo)
    {
        skipTo = 0;
        if (body is not [(byte)'<', _, ..] || VideoUrlProbe.MatchHttpSchemeLength(body[1..]) > 0)
        {
            return false;
        }

        if (VideoMarkdownScanner.StartsWithHtmlTag(body, "script"u8))
        {
            skipTo = VideoMarkdownScanner.ConsumeUntilCloseTag(source, trimmedStart, "</script>"u8);
            return true;
        }

        if (VideoMarkdownScanner.StartsWithHtmlTag(body, "style"u8))
        {
            skipTo = VideoMarkdownScanner.ConsumeUntilCloseTag(source, trimmedStart, "</style>"u8);
            return true;
        }

        if (VideoMarkdownScanner.StartsWithHtmlTag(body, "pre"u8))
        {
            skipTo = VideoMarkdownScanner.ConsumeUntilCloseTag(source, trimmedStart, "</pre>"u8);
            return true;
        }

        if (VideoMarkdownScanner.StartsWithHtmlTag(body, "textarea"u8))
        {
            skipTo = VideoMarkdownScanner.ConsumeUntilCloseTag(source, trimmedStart, "</textarea>"u8);
            return true;
        }

        var lineEnd = Utf8LineSpan.FindLineEnd(source, trimmedStart);
        skipTo = Utf8LineSpan.AdvancePastLineTerminator(source, lineEnd);
        return true;
    }

    /// <summary>Attempts to rewrite a line whose entire trimmed content is a video URL, autolink, <c>![alt](url)</c>, or (when enabled) <c>[title](url)</c>.</summary>
    /// <param name="session">Active rewrite session.</param>
    /// <param name="lineStart">Start offset of the line.</param>
    /// <param name="nextLineStart">Start offset of the following line.</param>
    /// <param name="line">Line slice without terminator.</param>
    /// <returns><c>true</c> when the standalone line was rewritten into a video player block.</returns>
    private static bool TryRewriteStandaloneLine(
        ref RewriteSession session,
        int lineStart,
        int nextLineStart,
        ReadOnlySpan<byte> line)
    {
        var trimmed = AsciiByteHelpers.TrimHorizontalWhitespace(line);
        if (!TryExtractStandaloneCandidate(trimmed, session.Options, out var urlSlice, out var titleSlice)
            || !VideoUrlParser.TryParse(urlSlice, session.Options.Providers, out var match))
        {
            return false;
        }

        EmitStandaloneReplacement(ref session, lineStart, nextLineStart, match, titleSlice);
        return true;
    }

    /// <summary>Extracts the candidate URL and title from a standalone trimmed line.</summary>
    /// <param name="trimmed">Whitespace-trimmed line bytes (caller guarantees non-empty).</param>
    /// <param name="options">Configured video options.</param>
    /// <param name="url">Extracted candidate URL.</param>
    /// <param name="title">Extracted candidate accessible title.</param>
    /// <returns><c>true</c> when the line has a standalone candidate shape.</returns>
    private static bool TryExtractStandaloneCandidate(
        ReadOnlySpan<byte> trimmed,
        in VideoOptions options,
        out ReadOnlySpan<byte> url,
        out ReadOnlySpan<byte> title)
    {
        url = default;
        title = default;
        if (trimmed is [(byte)'!', (byte)'[', ..])
        {
            return options.EmbedImageLinks
                   && TryExtractCompleteMarkdownLink(trimmed, 1, out url, out title);
        }

        return trimmed is [(byte)'[', ..]
            ? options.EmbedStandaloneLinks
              && TryExtractCompleteMarkdownLink(trimmed, 0, out url, out title)
            : options.EmbedBareUrls && VideoMarkdownScanner.TryExtractBareOrAutolinkUrl(trimmed, out url);
    }

    /// <summary>Extracts a <c>[label](href "title")</c> link that spans through the end of <paramref name="trimmed"/>.</summary>
    /// <param name="trimmed">Trimmed line slice.</param>
    /// <param name="bracketStart">Offset of the opening <c>[</c> byte.</param>
    /// <param name="url">Extracted destination URL.</param>
    /// <param name="title">Explicit link title when non-empty; otherwise the link label.</param>
    /// <returns><c>true</c> when the link covers the remainder of <paramref name="trimmed"/>.</returns>
    private static bool TryExtractCompleteMarkdownLink(
        ReadOnlySpan<byte> trimmed,
        int bracketStart,
        out ReadOnlySpan<byte> url,
        out ReadOnlySpan<byte> title)
    {
        url = default;
        title = default;
        if (!VideoMarkdownScanner.TryReadMarkdownLink(trimmed, bracketStart, out var label, out var href, out var explicitTitle, out var end)
            || end != trimmed.Length)
        {
            return false;
        }

        url = href;
        title = explicitTitle.IsEmpty ? label : explicitTitle;
        return true;
    }

    /// <summary>Rewrites inline <c>![alt](url)</c> video links inside a paragraph/list line while skipping inline backtick code spans.</summary>
    /// <param name="session">Active rewrite session.</param>
    /// <param name="lineStart">Start offset of the line.</param>
    /// <param name="lineEnd">End offset of the line.</param>
    private static void RewriteInlineMediaImages(ref RewriteSession session, int lineStart, int lineEnd)
    {
        var source = session.Source;
        var cursor = lineStart;
        while (cursor < lineEnd)
        {
            var b = source[cursor];
            if (b is (byte)'\\')
            {
                cursor += EscapeSequenceLength;
                continue;
            }

            if (b is (byte)'`')
            {
                cursor = MarkdownCodeScanner.ConsumeInlineCode(source, cursor);
                continue;
            }

            if (b is (byte)'!' && TryEmitInlineMediaImage(ref session, cursor, lineEnd, out var linkEnd))
            {
                cursor = linkEnd;
                continue;
            }

            cursor++;
        }
    }

    /// <summary>Attempts to parse and emit a single inline <c>![alt](url)</c> media image starting at <paramref name="bangOffset"/>.</summary>
    /// <param name="session">Active rewrite session.</param>
    /// <param name="bangOffset">Offset of the <c>!</c> byte.</param>
    /// <param name="lineEnd">End offset of the enclosing line.</param>
    /// <param name="linkEnd">End offset immediately past <c>)</c> when emitted.</param>
    /// <returns><c>true</c> when an inline video image link was emitted.</returns>
    private static bool TryEmitInlineMediaImage(
        ref RewriteSession session,
        int bangOffset,
        int lineEnd,
        out int linkEnd)
    {
        linkEnd = 0;
        var source = session.Source;
        if (bangOffset + 1 >= lineEnd
            || source[bangOffset + 1] is not (byte)'['
            || !VideoMarkdownScanner.TryReadMarkdownLink(source[..lineEnd], bangOffset + 1, out var label, out var href, out var explicitTitle, out linkEnd)
            || !VideoUrlParser.TryParse(href, session.Options.Providers, out var match))
        {
            return false;
        }

        if (bangOffset > session.LastEmit)
        {
            Utf8StringWriter.Write(session.Writer, source[session.LastEmit..bangOffset]);
        }

        WriteBlockSeparatorBefore(source, session.LastEmit, bangOffset, session.Writer);
        var title = explicitTitle.IsEmpty ? label : explicitTitle;
        VideoEmbedEmitter.Emit(match, title, session.Options, session.TwitchParent, session.Writer);
        Utf8StringWriter.Write(session.Writer, "\n\n"u8);
        session.LastEmit = linkEnd;
        return true;
    }

    /// <summary>Flushes buffered source before <paramref name="lineStart"/> and writes the standalone HTML block surrounded by blank lines.</summary>
    /// <param name="session">Active rewrite session.</param>
    /// <param name="lineStart">Start offset of the replaced line.</param>
    /// <param name="nextLineStart">Start offset of the next line.</param>
    /// <param name="match">Matched video target.</param>
    /// <param name="titleSlice">Optional caller-supplied accessible title.</param>
    private static void EmitStandaloneReplacement(
        ref RewriteSession session,
        int lineStart,
        int nextLineStart,
        in VideoMatch match,
        ReadOnlySpan<byte> titleSlice)
    {
        if (lineStart > session.LastEmit)
        {
            Utf8StringWriter.Write(session.Writer, session.Source[session.LastEmit..lineStart]);
        }

        WriteBlockSeparatorBefore(session.Source, session.LastEmit, lineStart, session.Writer);
        VideoEmbedEmitter.Emit(match, titleSlice, session.Options, session.TwitchParent, session.Writer);
        Utf8StringWriter.Write(session.Writer, "\n\n"u8);
        session.LastEmit = nextLineStart;
    }

    /// <summary>Ensures a blank line precedes an injected CommonMark Type-6 <c>&lt;div&gt;</c> block when prior text was emitted.</summary>
    /// <param name="source">Full UTF-8 markdown buffer.</param>
    /// <param name="lastEmit">Offset of the previous emitted replacement end.</param>
    /// <param name="matchStart">Start offset of the current replacement.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteBlockSeparatorBefore(
        ReadOnlySpan<byte> source,
        int lastEmit,
        int matchStart,
        IBufferWriter<byte> writer)
    {
        if (matchStart is 0)
        {
            return;
        }

        var prev = source[matchStart - 1];
        if (prev is not (byte)'\n')
        {
            Utf8StringWriter.Write(writer, "\n\n"u8);
            return;
        }

        // One newline is already in the output; ensure a second so any preceding paragraph closes cleanly.
        var beforePrev = matchStart >= TwoBytesBack ? source[matchStart - TwoBytesBack] : (byte)'\n';
        if (beforePrev is (byte)'\r')
        {
            beforePrev = matchStart >= ThreeBytesBack ? source[matchStart - ThreeBytesBack] : (byte)'\n';
        }

        if (beforePrev is (byte)'\n' || matchStart <= lastEmit)
        {
            return;
        }

        Utf8StringWriter.WriteByte(writer, (byte)'\n');
    }

    /// <summary>Mutable zero-allocation cursor state shared across <see cref="Rewrite"/> helpers.</summary>
    [System.Diagnostics.DebuggerDisplay("RewriteSession: LastEmit={LastEmit}")]
    private ref struct RewriteSession
    {
        /// <summary>Initializes a new instance of the <see cref="RewriteSession"/> struct.</summary>
        /// <param name="source">Full UTF-8 markdown buffer.</param>
        /// <param name="writer">Destination UTF-8 sink.</param>
        /// <param name="options">Configured video options.</param>
        /// <param name="twitchParent">Resolved Twitch parent host.</param>
        internal RewriteSession(
            ReadOnlySpan<byte> source,
            IBufferWriter<byte> writer,
            in VideoOptions options,
            ReadOnlySpan<byte> twitchParent)
        {
            Source = source;
            Writer = writer;
            Options = options;
            TwitchParent = twitchParent;
            LastEmit = 0;
        }

        /// <summary>Gets the full UTF-8 markdown buffer.</summary>
        internal readonly ReadOnlySpan<byte> Source { get; }

        /// <summary>Gets the destination UTF-8 sink.</summary>
        internal readonly IBufferWriter<byte> Writer { get; }

        /// <summary>Gets the configured video options.</summary>
        internal readonly VideoOptions Options { get; }

        /// <summary>Gets the resolved Twitch parent host.</summary>
        internal readonly ReadOnlySpan<byte> TwitchParent { get; }

        /// <summary>Gets or sets the byte offset in <see cref="Source"/> up to which bytes have already been emitted.</summary>
        internal int LastEmit { get; set; }
    }
}
