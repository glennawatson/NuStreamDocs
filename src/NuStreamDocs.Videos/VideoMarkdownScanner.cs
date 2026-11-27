// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Videos;

/// <summary>Zero-allocation UTF-8 Markdown link, indentation, and HTML block scanners used by <see cref="VideoRewriter"/>.</summary>
internal static class VideoMarkdownScanner
{
    /// <summary>Indent column count that opens a CommonMark indented code block.</summary>
    internal const int IndentedCodeColumn = 4;

    /// <summary>Length of the <c>](</c> separator between a link label and destination.</summary>
    private const int LabelDestinationSeparatorLength = 2;

    /// <summary>Offset before the closing quote when scanning backward for an opening link-title quote.</summary>
    private const int ClosingQuoteOffset = 2;

    /// <summary>Sentence punctuation stripped from the end of a bare standalone URL.</summary>
    private static readonly SearchValues<byte> TrailingUrlPunctuation =
        SearchValues.Create(".,;:!?"u8);

    /// <summary>Extracts an HTTP(S) URL from either a <c>&lt;https://...&gt;</c> autolink or a bare URL line.</summary>
    /// <param name="trimmed">Trimmed line slice.</param>
    /// <param name="url">Extracted URL slice.</param>
    /// <returns><c>true</c> when <paramref name="trimmed"/> begins with an HTTP(S) URL or autolink.</returns>
    internal static bool TryExtractBareOrAutolinkUrl(ReadOnlySpan<byte> trimmed, out ReadOnlySpan<byte> url)
    {
        url = default;
        if (trimmed is [(byte)'<', .., (byte)'>'])
        {
            var inner = trimmed[1..^1];
            if (VideoUrlProbe.MatchHttpSchemeLength(inner) is 0)
            {
                return false;
            }

            url = inner;
            return true;
        }

        if (VideoUrlProbe.MatchHttpSchemeLength(trimmed) is 0)
        {
            return false;
        }

        url = TrimTrailingSentencePunctuation(trimmed);
        return true;
    }

    /// <summary>Parses <c>[label](href "optional title")</c> starting at the <c>[</c> byte at <paramref name="bracketStart"/>.</summary>
    /// <param name="span">Enclosing line or document slice.</param>
    /// <param name="bracketStart">Offset of the opening <c>[</c> byte.</param>
    /// <param name="label">Extracted bracket label.</param>
    /// <param name="href">Extracted link destination.</param>
    /// <param name="title">Extracted optional quoted link title.</param>
    /// <param name="endOffset">Offset immediately past the closing <c>)</c> byte.</param>
    /// <returns><c>true</c> when a well-formed Markdown inline link with a non-empty destination was read.</returns>
    internal static bool TryReadMarkdownLink(
        ReadOnlySpan<byte> span,
        int bracketStart,
        out ReadOnlySpan<byte> label,
        out ReadOnlySpan<byte> href,
        out ReadOnlySpan<byte> title,
        out int endOffset)
    {
        label = default;
        href = default;
        title = default;
        endOffset = 0;

        var labelClose = FindBalancedClose(span, bracketStart + 1, (byte)'[', (byte)']');
        if (labelClose < 0 || labelClose + 1 >= span.Length || span[labelClose + 1] is not (byte)'(')
        {
            return false;
        }

        var contentStart = labelClose + LabelDestinationSeparatorLength;
        var parenClose = FindBalancedClose(span, contentStart, (byte)'(', (byte)')');
        if (parenClose < 0)
        {
            return false;
        }

        label = span[(bracketStart + 1)..labelClose];
        SplitDestinationAndTitle(span[contentStart..parenClose], out href, out title);
        endOffset = parenClose + 1;
        return !href.IsEmpty;
    }

    /// <summary>Splits a Markdown link destination inner slice into <paramref name="href"/> and optional quoted <paramref name="title"/>.</summary>
    /// <param name="inner">Slice inside <c>(...)</c>.</param>
    /// <param name="href">Extracted URL slice.</param>
    /// <param name="title">Extracted quoted title slice, or empty.</param>
    internal static void SplitDestinationAndTitle(
        ReadOnlySpan<byte> inner,
        out ReadOnlySpan<byte> href,
        out ReadOnlySpan<byte> title)
    {
        title = default;
        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(inner);
        if (trimmed.IsEmpty)
        {
            href = default;
            return;
        }

        if (TryStripTrailingQuotedTitle(trimmed, out var beforeTitle, out var extractedTitle))
        {
            trimmed = beforeTitle;
            title = extractedTitle;
        }

        href = trimmed is [(byte)'<', .., (byte)'>'] ? trimmed[1..^1] : trimmed;
    }

    /// <summary>Peels a trailing single- or double-quoted link title preceded by whitespace.</summary>
    /// <param name="trimmed">Whitespace-trimmed destination content.</param>
    /// <param name="beforeTitle">Trimmed destination before the quoted title.</param>
    /// <param name="title">Unquoted title content.</param>
    /// <returns><c>true</c> when a trailing quoted title was separated.</returns>
    internal static bool TryStripTrailingQuotedTitle(
        ReadOnlySpan<byte> trimmed,
        out ReadOnlySpan<byte> beforeTitle,
        out ReadOnlySpan<byte> title)
    {
        beforeTitle = default;
        title = default;
        if (trimmed.Length < ClosingQuoteOffset || trimmed[^1] is not ((byte)'"' or (byte)'\''))
        {
            return false;
        }

        var quote = trimmed[^1];
        for (var i = trimmed.Length - ClosingQuoteOffset; i > 0; i--)
        {
            if (trimmed[i] != quote || !AsciiByteHelpers.IsAsciiWhitespace(trimmed[i - 1]))
            {
                continue;
            }

            title = trimmed[(i + 1)..^1];
            beforeTitle = AsciiByteHelpers.TrimAsciiWhitespace(trimmed[..i]);
            return true;
        }

        return false;
    }

    /// <summary>Finds a balanced closing delimiter while skipping backslash-escaped characters.</summary>
    /// <param name="span">Slice to scan.</param>
    /// <param name="from">Offset immediately past the opening delimiter.</param>
    /// <param name="open">Opening delimiter byte.</param>
    /// <param name="close">Closing delimiter byte.</param>
    /// <returns>Offset of the matching closing byte, or <c>-1</c>.</returns>
    internal static int FindBalancedClose(ReadOnlySpan<byte> span, int from, byte open, byte close)
    {
        var depth = 1;
        for (var i = from; i < span.Length; i++)
        {
            var b = span[i];
            if (b is (byte)'\\')
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
            if (depth is 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Strips trailing sentence punctuation from a bare standalone URL.</summary>
    /// <param name="url">Candidate URL bytes.</param>
    /// <returns>Trimmed URL slice.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> TrimTrailingSentencePunctuation(ReadOnlySpan<byte> url)
    {
        var end = url.Length;
        while (end > 0 && TrailingUrlPunctuation.Contains(url[end - 1]))
        {
            end--;
        }

        return url[..end];
    }

    /// <summary>Counts leading spaces and reports whether a leading tab occurred within the first four columns.</summary>
    /// <param name="line">Line slice without terminator.</param>
    /// <param name="hasLeadingTab"><c>true</c> when a tab appeared before column 4.</param>
    /// <returns>Count of leading <c>0x20</c> space bytes.</returns>
    internal static int CountLeadingSpaces(ReadOnlySpan<byte> line, out bool hasLeadingTab)
    {
        var count = 0;
        while (count < line.Length && line[count] is (byte)' ')
        {
            count++;
        }

        hasLeadingTab = count < line.Length && count < IndentedCodeColumn && line[count] is (byte)'\t';
        return count;
    }

    /// <summary>Consumes consecutive indented-code and blank lines until the next unindented block.</summary>
    /// <param name="source">Full UTF-8 markdown buffer.</param>
    /// <param name="start">Start offset of the indented code block.</param>
    /// <returns>Offset of the first line after the indented code block.</returns>
    internal static int ConsumeIndentedCodeRun(ReadOnlySpan<byte> source, int start)
    {
        var pos = start;
        while (pos < source.Length)
        {
            var lineEnd = Utf8LineSpan.FindLineEnd(source, pos);
            var line = source[pos..lineEnd];
            if (!AsciiByteHelpers.IsAllAsciiWhitespace(line))
            {
                var indent = CountLeadingSpaces(line, out var tab);
                if (!tab && indent < IndentedCodeColumn)
                {
                    return pos;
                }
            }

            pos = Utf8LineSpan.AdvancePastLineTerminator(source, lineEnd);
        }

        return source.Length;
    }

    /// <summary>Consumes lines through the line containing <paramref name="token"/>.</summary>
    /// <param name="source">Full UTF-8 markdown buffer.</param>
    /// <param name="searchFrom">Offset at which to begin searching for <paramref name="token"/>.</param>
    /// <param name="token">Closing token bytes (such as <c>--&gt;</c>).</param>
    /// <returns>Offset immediately past the line containing <paramref name="token"/>, or <c>source.Length</c>.</returns>
    internal static int ConsumeUntilToken(ReadOnlySpan<byte> source, int searchFrom, ReadOnlySpan<byte> token)
    {
        var rel = source[searchFrom..].IndexOf(token);
        if (rel < 0)
        {
            return source.Length;
        }

        var lineEnd = Utf8LineSpan.FindLineEnd(source, searchFrom + rel + token.Length);
        return Utf8LineSpan.AdvancePastLineTerminator(source, lineEnd);
    }

    /// <summary>Consumes lines through the line containing <paramref name="lowerCloseTag"/> (case-insensitive).</summary>
    /// <param name="source">Full UTF-8 markdown buffer.</param>
    /// <param name="searchFrom">Offset of the opening tag.</param>
    /// <param name="lowerCloseTag">Lowercase closing tag bytes (such as <c>&lt;/script&gt;</c>).</param>
    /// <returns>Offset immediately past the line containing the closing tag, or <c>source.Length</c>.</returns>
    internal static int ConsumeUntilCloseTag(ReadOnlySpan<byte> source, int searchFrom, ReadOnlySpan<byte> lowerCloseTag)
    {
        for (var i = searchFrom; i + lowerCloseTag.Length <= source.Length; i++)
        {
            if (source[i] is not (byte)'<' || !AsciiByteHelpers.StartsWithIgnoreAsciiCase(source, i, lowerCloseTag))
            {
                continue;
            }

            var lineEnd = Utf8LineSpan.FindLineEnd(source, i + lowerCloseTag.Length);
            return Utf8LineSpan.AdvancePastLineTerminator(source, lineEnd);
        }

        return source.Length;
    }

    /// <summary>True when <paramref name="body"/> opens an HTML tag with <paramref name="lowerName"/>.</summary>
    /// <param name="body">Line slice starting with <c>&lt;</c>.</param>
    /// <param name="lowerName">Lowercase HTML tag name.</param>
    /// <returns><c>true</c> when <paramref name="body"/> opens <paramref name="lowerName"/>.</returns>
    internal static bool StartsWithHtmlTag(ReadOnlySpan<byte> body, ReadOnlySpan<byte> lowerName)
    {
        var afterName = 1 + lowerName.Length;
        if (body.Length < afterName || !AsciiByteHelpers.StartsWithIgnoreAsciiCase(body, 1, lowerName))
        {
            return false;
        }

        return afterName == body.Length
               || body[afterName] is (byte)' ' or (byte)'\t' or (byte)'>' or (byte)'/';
    }
}
