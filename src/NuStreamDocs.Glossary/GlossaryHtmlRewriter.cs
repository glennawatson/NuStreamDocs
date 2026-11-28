// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Glossary;

/// <summary>Rewrites rendered HTML prose to wrap whole-word glossary terms in accessible <c>&lt;abbr&gt;</c> tags outside headings, links, and code.</summary>
internal static class GlossaryHtmlRewriter
{
    /// <summary>Length of a 2-byte heading tag name such as <c>h1</c>.</summary>
    private const int HeadingTagLength = 2;

    /// <summary>Reverse index of <c>/</c> in a self-closing <c>.../&gt;</c> tag.</summary>
    private const int SelfClosingSlashReverseOffset = 2;

    /// <summary>Lowercase tag names whose text content must never be auto-linked.</summary>
    private static readonly byte[][] ForbiddenTagNames =
    [
        [.. "a"u8],
        [.. "code"u8],
        [.. "pre"u8],
        [.. "head"u8],
        [.. "script"u8],
        [.. "style"u8],
        [.. "abbr"u8],
    ];

    /// <summary>Checks whether <paramref name="html"/> contains at least one body tag candidate worth scanning.</summary>
    /// <param name="html">Rendered HTML bytes.</param>
    /// <param name="entries">Longest-first glossary entries.</param>
    /// <returns><c>true</c> when entries exist and <paramref name="html"/> is non-empty.</returns>
    internal static bool NeedsRewrite(ReadOnlySpan<byte> html, GlossaryEntry[] entries) =>
        entries.Length > 0 && !html.IsEmpty;

    /// <summary>Rewrites <paramref name="html"/> into <paramref name="output"/>, wrapping matching glossary terms in <c>&lt;abbr&gt;</c> elements.</summary>
    /// <param name="html">Rendered HTML bytes.</param>
    /// <param name="entries">Longest-first glossary entries.</param>
    /// <param name="glossaryBaseUrl">Base URL of the glossary page (for example <c>/glossary/</c>), or empty when disabled.</param>
    /// <param name="firstOccurrenceOnly">When <c>true</c>, wraps each term at most once per page.</param>
    /// <param name="output">Destination buffer.</param>
    internal static void Rewrite(
        ReadOnlySpan<byte> html,
        GlossaryEntry[] entries,
        ReadOnlySpan<byte> glossaryBaseUrl,
        bool firstOccurrenceOnly,
        IBufferWriter<byte> output)
    {
        var seen = firstOccurrenceOnly ? new bool[entries.Length] : null;
        var skipDepth = 0;
        var cursor = 0;

        while (cursor < html.Length)
        {
            var openTagOffset = html[cursor..].IndexOf((byte)'<');
            if (openTagOffset < 0)
            {
                WriteSegment(html[cursor..], skipDepth > 0, entries, glossaryBaseUrl, seen, output);
                break;
            }

            if (openTagOffset > 0)
            {
                WriteSegment(html.Slice(cursor, openTagOffset), skipDepth > 0, entries, glossaryBaseUrl, seen, output);
                cursor += openTagOffset;
            }

            var closeTagOffset = html[cursor..].IndexOf((byte)'>');
            if (closeTagOffset < 0)
            {
                output.Write(html[cursor..]);
                break;
            }

            var tagSpan = html.Slice(cursor, closeTagOffset + 1);
            UpdateSkipDepth(tagSpan, ref skipDepth);
            output.Write(tagSpan);
            cursor += closeTagOffset + 1;
        }
    }

    /// <summary>Writes a text run, either verbatim (inside skipped tags) or scanning for whole-word glossary terms.</summary>
    /// <param name="text">Plain HTML text slice between tags.</param>
    /// <param name="skip">Whether currently inside a forbidden element.</param>
    /// <param name="entries">Longest-first glossary entries.</param>
    /// <param name="glossaryBaseUrl">Glossary base URL.</param>
    /// <param name="seen">Optional per-page first-occurrence tracker.</param>
    /// <param name="output">Destination buffer.</param>
    private static void WriteSegment(
        ReadOnlySpan<byte> text,
        bool skip,
        GlossaryEntry[] entries,
        ReadOnlySpan<byte> glossaryBaseUrl,
        bool[]? seen,
        IBufferWriter<byte> output)
    {
        if (skip)
        {
            output.Write(text);
            return;
        }

        var flushed = 0;
        var idx = 0;
        while (idx < text.Length)
        {
            if ((idx == 0 || !IsWordByte(text[idx - 1]))
                && TryMatchEntry(text, idx, entries, seen, out var matchIdx, out var matchLen))
            {
                output.Write(text.Slice(flushed, idx - flushed));
                WriteAbbrElement(output, text.Slice(idx, matchLen), entries[matchIdx], glossaryBaseUrl);
                idx += matchLen;
                flushed = idx;
                continue;
            }

            idx++;
        }

        if (flushed < text.Length)
        {
            output.Write(text[flushed..]);
        }
    }

    /// <summary>Attempts to match the longest eligible glossary entry at <paramref name="offset"/> ending on a word boundary.</summary>
    /// <param name="text">Text slice.</param>
    /// <param name="offset">Candidate start offset.</param>
    /// <param name="entries">Longest-first glossary entries.</param>
    /// <param name="seen">Optional per-page first-occurrence flags.</param>
    /// <param name="matchIndex">Matched entry index.</param>
    /// <param name="matchLength">Matched byte length.</param>
    /// <returns><c>true</c> when a whole-word term matched.</returns>
    private static bool TryMatchEntry(
        ReadOnlySpan<byte> text,
        int offset,
        GlossaryEntry[] entries,
        bool[]? seen,
        out int matchIndex,
        out int matchLength)
    {
        for (var i = 0; i < entries.Length; i++)
        {
            if (seen is not null && seen[i])
            {
                continue;
            }

            var term = entries[i].Term;
            if (offset + term.Length > text.Length)
            {
                continue;
            }

            var end = offset + term.Length;
            if (end < text.Length && IsWordByte(text[end]))
            {
                continue;
            }

            if (!AsciiEqualsIgnoreCase(text.Slice(offset, term.Length), term))
            {
                continue;
            }

            if (seen is not null)
            {
                seen[i] = true;
            }

            matchIndex = i;
            matchLength = term.Length;
            return true;
        }

        matchIndex = -1;
        matchLength = 0;
        return false;
    }

    /// <summary>Emits <c>&lt;abbr class="nd-glossary-term" tabindex="0" title="..."&gt;...&lt;/abbr&gt;</c>.</summary>
    /// <param name="output">Destination buffer.</param>
    /// <param name="matchedText">Original-case matched text slice.</param>
    /// <param name="entry">Glossary entry metadata.</param>
    /// <param name="glossaryBaseUrl">Optional glossary base URL.</param>
    private static void WriteAbbrElement(
        IBufferWriter<byte> output,
        ReadOnlySpan<byte> matchedText,
        in GlossaryEntry entry,
        ReadOnlySpan<byte> glossaryBaseUrl)
    {
        output.Write("<abbr class=\"nd-glossary-term\" tabindex=\"0\" title=\""u8);
        WriteHtmlAttributeEscaped(output, entry.Definition);
        if (!glossaryBaseUrl.IsEmpty)
        {
            output.Write("\" data-glossary-href=\""u8);
            output.Write(glossaryBaseUrl);
            output.Write("#"u8);
            output.Write(entry.Slug);
        }

        output.Write("\">"u8);
        output.Write(matchedText);
        output.Write("</abbr>"u8);
    }

    /// <summary>Writes <paramref name="value"/> escaping <c>&amp;</c>, <c>"</c>, <c>&lt;</c>, and <c>&gt;</c>.</summary>
    /// <param name="output">Destination buffer.</param>
    /// <param name="value">UTF-8 attribute bytes.</param>
    private static void WriteHtmlAttributeEscaped(IBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if (b == (byte)'&')
            {
                output.Write("&amp;"u8);
            }
            else if (b == (byte)'"')
            {
                output.Write("&quot;"u8);
            }
            else if (b == (byte)'<')
            {
                output.Write("&lt;"u8);
            }
            else if (b == (byte)'>')
            {
                output.Write("&gt;"u8);
            }
            else
            {
                var dst = output.GetSpan(1);
                dst[0] = b;
                output.Advance(1);
            }
        }
    }

    /// <summary>Updates <paramref name="skipDepth"/> when entering or leaving a tag whose text must not be auto-linked.</summary>
    /// <param name="tagSpan">Complete <c>&lt;...&gt;</c> tag slice.</param>
    /// <param name="skipDepth">Active nesting counter.</param>
    private static void UpdateSkipDepth(ReadOnlySpan<byte> tagSpan, ref int skipDepth)
    {
        if (IsDeclarationOrSelfClosing(tagSpan))
        {
            return;
        }

        var closing = tagSpan[1] == (byte)'/';
        var nameStart = closing ? HeadingTagLength : 1;
        var nameEnd = nameStart;
        while (IsWordByte(tagSpan[nameEnd]))
        {
            nameEnd++;
        }

        if (!IsSkippedTagName(tagSpan[nameStart..nameEnd]))
        {
            return;
        }

        ApplySkipDelta(closing, ref skipDepth);
    }

    /// <summary>Checks whether <paramref name="tagSpan"/> is a comment, processing instruction, empty bracket pair, or self-closing element.</summary>
    /// <param name="tagSpan">Complete <c>&lt;...&gt;</c> slice.</param>
    /// <returns><c>true</c> when the tag cannot change nesting depth.</returns>
    private static bool IsDeclarationOrSelfClosing(ReadOnlySpan<byte> tagSpan) =>
        tagSpan.Length <= HeadingTagLength
        || tagSpan[1] is (byte)'!' or (byte)'?'
        || tagSpan[^SelfClosingSlashReverseOffset] == (byte)'/';

    /// <summary>Increments or decrements <paramref name="skipDepth"/> for a matched forbidden element.</summary>
    /// <param name="closing"><c>true</c> for a closing tag.</param>
    /// <param name="skipDepth">Current skip depth.</param>
    private static void ApplySkipDelta(bool closing, ref int skipDepth)
    {
        if (!closing)
        {
            skipDepth++;
            return;
        }

        if (skipDepth > 0)
        {
            skipDepth--;
        }
    }

    /// <summary>Checks whether <paramref name="tagName"/> is a heading, code, anchor, script, style, head, or abbr element.</summary>
    /// <param name="tagName">Element name slice.</param>
    /// <returns><c>true</c> when prose inside this element must not be wrapped.</returns>
    private static bool IsSkippedTagName(ReadOnlySpan<byte> tagName)
    {
        if (tagName.Length == HeadingTagLength
            && AsciiByteHelpers.ToAsciiLowerByte(tagName[0]) == (byte)'h'
            && tagName[1] is >= (byte)'1' and <= (byte)'6')
        {
            return true;
        }

        for (var i = 0; i < ForbiddenTagNames.Length; i++)
        {
            if (AsciiEqualsIgnoreCase(tagName, ForbiddenTagNames[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks whether <paramref name="b"/> is an ASCII word character.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns><c>true</c> for ASCII letters, digits, or underscore.</returns>
    private static bool IsWordByte(byte b) =>
        AsciiByteHelpers.IsAsciiLetter(b) || AsciiByteHelpers.IsAsciiDigit(b) || b == (byte)'_';

    /// <summary>Compares equal-length spans ASCII case-insensitively.</summary>
    /// <param name="left">Candidate span.</param>
    /// <param name="right">Target span.</param>
    /// <returns><c>true</c> when equal ignoring ASCII case.</returns>
    private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (AsciiByteHelpers.ToAsciiLowerByte(left[i]) != AsciiByteHelpers.ToAsciiLowerByte(right[i]))
            {
                return false;
            }
        }

        return true;
    }
}
