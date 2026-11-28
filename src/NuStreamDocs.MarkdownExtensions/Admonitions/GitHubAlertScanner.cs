// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.MarkdownExtensions.Admonitions;

/// <summary>Scans and strips GitHub blockquote alerts (<c>&gt; [!NOTE]</c>, <c>&gt; [!TIP]</c>, <c>&gt; [!IMPORTANT]</c>, <c>&gt; [!WARNING]</c>, <c>&gt; [!CAUTION]</c>).</summary>
internal static class GitHubAlertScanner
{
    /// <summary>Maximum leading spaces before a CommonMark blockquote <c>&gt;</c> marker.</summary>
    private const int MaxLeadingSpaces = 3;

    /// <summary>Length of the <c>[!</c> opening alert prefix.</summary>
    private const int AlertPrefixLength = 2;

    /// <summary>Recognized GitHub alert definitions.</summary>
    private static readonly AlertDescriptor[] KnownAlerts =
    [
        new([.. "note"u8], [.. "Note"u8]),
        new([.. "tip"u8], [.. "Tip"u8]),
        new([.. "important"u8], [.. "Important"u8]),
        new([.. "warning"u8], [.. "Warning"u8]),
        new([.. "caution"u8], [.. "Caution"u8]),
    ];

    /// <summary>Tries to parse a GitHub alert blockquote starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 Markdown source.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <param name="cssType">Lowercase admonition CSS class bytes.</param>
    /// <param name="title">Display title bytes.</param>
    /// <param name="headerEnd">Exclusive end of the <c>&gt; [!TYPE]</c> opener line.</param>
    /// <param name="bodyEnd">Exclusive end of consecutive blockquote body lines.</param>
    /// <returns><c>true</c> when <paramref name="offset"/> starts a valid GitHub alert.</returns>
    internal static bool TryParseAlert(
        ReadOnlySpan<byte> source,
        int offset,
        out ReadOnlySpan<byte> cssType,
        out ReadOnlySpan<byte> title,
        out int headerEnd,
        out int bodyEnd)
    {
        cssType = default;
        title = default;
        headerEnd = 0;
        bodyEnd = 0;

        if (!TryReadAlertToken(source, offset, out var token, out var afterBracket))
        {
            return false;
        }

        if (!TryResolveAlertKind(token, out cssType, out title))
        {
            return false;
        }

        if (!TryAdvanceToLineEnd(source, afterBracket, out headerEnd))
        {
            return false;
        }

        bodyEnd = ConsumeBlockquoteLines(source, headerEnd);
        return true;
    }

    /// <summary>Strips the blockquote prefix from each line in <paramref name="body"/> and writes the result to <paramref name="writer"/>.</summary>
    /// <param name="body">Blockquote body slice.</param>
    /// <param name="writer">Destination buffer.</param>
    internal static void WriteStrippedQuoteBody(ReadOnlySpan<byte> body, IBufferWriter<byte> writer)
    {
        var cursor = 0;
        while (cursor < body.Length)
        {
            var lineEnd = Utf8LineSpan.LfLineEnd(body, cursor);
            var line = body[cursor..lineEnd];
            _ = TrySkipQuoteMarker(line, 0, out var contentStart);
            if (contentStart < line.Length && line[contentStart] is (byte)' ' or (byte)'\t')
            {
                contentStart++;
            }

            writer.Write(line[contentStart..]);
            cursor = lineEnd;
        }
    }

    /// <summary>Reads the <c>[!TOKEN]</c> slice after a leading blockquote marker.</summary>
    /// <param name="source">Markdown source.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <param name="token">Extracted alert token.</param>
    /// <param name="afterBracket">Offset immediately past <c>]</c>.</param>
    /// <returns><c>true</c> when a bracketed alert marker was found.</returns>
    private static bool TryReadAlertToken(
        ReadOnlySpan<byte> source,
        int offset,
        out ReadOnlySpan<byte> token,
        out int afterBracket)
    {
        token = default;
        afterBracket = 0;

        if (!TrySkipQuoteMarker(source, offset, out var cursor))
        {
            return false;
        }

        cursor = SkipHorizontalWhitespace(source, cursor);
        if (cursor + AlertPrefixLength >= source.Length || !source[cursor..].StartsWith("[!"u8))
        {
            return false;
        }

        cursor += AlertPrefixLength;
        var closeBracketRel = source[cursor..].IndexOfAny((byte)']', (byte)'\n');
        if (closeBracketRel <= 0 || source[cursor + closeBracketRel] != (byte)']')
        {
            return false;
        }

        token = source.Slice(cursor, closeBracketRel);
        afterBracket = cursor + closeBracketRel + 1;
        return true;
    }

    /// <summary>Verifies only trailing whitespace/CR follows <c>]</c> up to LF or EOF.</summary>
    /// <param name="source">Markdown source.</param>
    /// <param name="cursor">Offset past <c>]</c>.</param>
    /// <param name="headerEnd">Offset past LF (or EOF).</param>
    /// <returns><c>true</c> when nothing else appears on the header line.</returns>
    private static bool TryAdvanceToLineEnd(ReadOnlySpan<byte> source, int cursor, out int headerEnd)
    {
        while (cursor < source.Length && source[cursor] is (byte)' ' or (byte)'\t' or (byte)'\r')
        {
            cursor++;
        }

        if (cursor < source.Length && source[cursor] != (byte)'\n')
        {
            headerEnd = 0;
            return false;
        }

        headerEnd = cursor < source.Length ? cursor + 1 : cursor;
        return true;
    }

    /// <summary>Advances past 0..3 leading spaces and a <c>&gt;</c> blockquote marker.</summary>
    /// <param name="source">Source span.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <param name="afterQuote">Offset immediately past <c>&gt;</c>.</param>
    /// <returns><c>true</c> when the line starts with a blockquote marker.</returns>
    private static bool TrySkipQuoteMarker(ReadOnlySpan<byte> source, int offset, out int afterQuote)
    {
        var spaces = 0;
        while (spaces < MaxLeadingSpaces && offset + spaces < source.Length && source[offset + spaces] == (byte)' ')
        {
            spaces++;
        }

        var markerIdx = offset + spaces;
        if (markerIdx < source.Length && source[markerIdx] == (byte)'>')
        {
            afterQuote = markerIdx + 1;
            return true;
        }

        afterQuote = 0;
        return false;
    }

    /// <summary>Skips spaces and tabs starting at <paramref name="cursor"/>.</summary>
    /// <param name="source">Source span.</param>
    /// <param name="cursor">Starting offset.</param>
    /// <returns>First non-space/tab offset.</returns>
    private static int SkipHorizontalWhitespace(ReadOnlySpan<byte> source, int cursor)
    {
        while (cursor < source.Length && source[cursor] is (byte)' ' or (byte)'\t')
        {
            cursor++;
        }

        return cursor;
    }

    /// <summary>Matches <paramref name="token"/> ASCII case-insensitively against the five GitHub alert names.</summary>
    /// <param name="token">Alert token inside <c>[!...]</c>.</param>
    /// <param name="cssType">Matched CSS class bytes.</param>
    /// <param name="title">Matched title bytes.</param>
    /// <returns><c>true</c> when recognized.</returns>
    private static bool TryResolveAlertKind(
        ReadOnlySpan<byte> token,
        out ReadOnlySpan<byte> cssType,
        out ReadOnlySpan<byte> title)
    {
        for (var i = 0; i < KnownAlerts.Length; i++)
        {
            var candidate = KnownAlerts[i];
            if (!AsciiEqualsIgnoreCase(token, candidate.CssType))
            {
                continue;
            }

            cssType = candidate.CssType;
            title = candidate.Title;
            return true;
        }

        cssType = default;
        title = default;
        return false;
    }

    /// <summary>Compares two spans ASCII case-insensitively where <paramref name="lowercaseTarget"/> is lowercase.</summary>
    /// <param name="input">Candidate span.</param>
    /// <param name="lowercaseTarget">Lowercase target bytes.</param>
    /// <returns><c>true</c> when equal ignoring ASCII case.</returns>
    private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> input, ReadOnlySpan<byte> lowercaseTarget)
    {
        if (input.Length != lowercaseTarget.Length)
        {
            return false;
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (AsciiByteHelpers.ToAsciiLowerByte(input[i]) != lowercaseTarget[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Consumes consecutive blockquote lines starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 Markdown source.</param>
    /// <param name="offset">Start of first body line.</param>
    /// <returns>Exclusive end of the blockquote block.</returns>
    private static int ConsumeBlockquoteLines(ReadOnlySpan<byte> source, int offset)
    {
        var cursor = offset;
        while (cursor < source.Length && TrySkipQuoteMarker(source, cursor, out _))
        {
            cursor = Utf8LineSpan.LfLineEnd(source, cursor);
        }

        return cursor;
    }

    /// <summary>Pairs a lowercase CSS class with its rendered title.</summary>
    /// <param name="CssType">Lowercase admonition class.</param>
    /// <param name="Title">Title-cased heading text.</param>
    private readonly record struct AlertDescriptor(byte[] CssType, byte[] Title);
}
