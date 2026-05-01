// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Yaml;

/// <summary>
/// Byte-level UTF-8 helpers for the small slice of YAML the
/// NuStreamDocs plugins actually parse — frontmatter delimiter walks,
/// top-level key inspection, scalar / list-token unquoting, and
/// trim-with-newlines. Centralised here because Tags, Nav, and
/// Metadata had been growing near-identical copies of every helper.
/// </summary>
public static class YamlByteScanner
{
    /// <summary>Whitespace bytes recognised inline (space + tab + carriage return).</summary>
    private static readonly SearchValues<byte> InlineWhitespace = SearchValues.Create(" \t\r"u8);

    /// <summary>Gets the UTF-8 bytes of the YAML frontmatter delimiter line (<c>---</c>).</summary>
    public static ReadOnlySpan<byte> FrontmatterDelimiter => "---"u8;

    /// <summary>
    /// Detects a complete <c>---</c>-delimited frontmatter block at the
    /// start of <paramref name="source"/> and reports both the offset of
    /// the closing <c>---</c> line and the offset just past its terminator.
    /// </summary>
    /// <param name="source">UTF-8 markdown bytes (frontmatter + body).</param>
    /// <param name="closerStart">Offset of the closing <c>---</c> line on success.</param>
    /// <param name="bodyStart">Offset of the first body byte (just past the closing delimiter line) on success.</param>
    /// <returns>True when a complete frontmatter block was found.</returns>
    public static bool TryFindFrontmatter(ReadOnlySpan<byte> source, out int closerStart, out int bodyStart)
    {
        closerStart = 0;
        bodyStart = 0;
        if (!source.StartsWith(FrontmatterDelimiter))
        {
            return false;
        }

        var afterFirst = FrontmatterDelimiter.Length;
        if (afterFirst >= source.Length || source[afterFirst] is not (byte)'\n' and not (byte)'\r')
        {
            return false;
        }

        var cursor = LineEnd(source, 0);
        while (cursor < source.Length)
        {
            var nextLineEnd = LineEnd(source, cursor);
            if (source[cursor..nextLineEnd].TrimEnd((byte)'\n').TrimEnd((byte)'\r').SequenceEqual(FrontmatterDelimiter))
            {
                closerStart = cursor;
                bodyStart = nextLineEnd;
                return true;
            }

            cursor = nextLineEnd;
        }

        return false;
    }

    /// <summary>Returns the offset just past the next newline, or <paramref name="source"/>.Length when none.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <returns>Inclusive line-end offset.</returns>
    public static int LineEnd(ReadOnlySpan<byte> source, int offset)
    {
        var rel = source[offset..].IndexOf((byte)'\n');
        return rel < 0 ? source.Length : offset + rel + 1;
    }

    /// <summary>Trims leading inline whitespace (space + tab + CR).</summary>
    /// <param name="span">Source bytes.</param>
    /// <returns>Slice with leading whitespace removed.</returns>
    public static ReadOnlySpan<byte> TrimLeading(ReadOnlySpan<byte> span)
    {
        var p = 0;
        while (p < span.Length && InlineWhitespace.Contains(span[p]))
        {
            p++;
        }

        return span[p..];
    }

    /// <summary>Trims leading + trailing inline whitespace plus newlines.</summary>
    /// <param name="span">Source bytes.</param>
    /// <returns>Slice with surrounding whitespace removed.</returns>
    public static ReadOnlySpan<byte> TrimWhitespace(ReadOnlySpan<byte> span)
    {
        var leading = TrimLeading(span);
        var p = leading.Length;
        while (p > 0 && (InlineWhitespace.Contains(leading[p - 1]) || leading[p - 1] is (byte)'\n'))
        {
            p--;
        }

        return leading[..p];
    }

    /// <summary>Strips matching YAML quote pairs (<c>"…"</c> or <c>'…'</c>) from <paramref name="span"/>.</summary>
    /// <param name="span">Source bytes.</param>
    /// <returns>Interior bytes when surrounded by matching quotes; passthrough otherwise.</returns>
    public static ReadOnlySpan<byte> Unquote(ReadOnlySpan<byte> span) =>
        span is [(byte)'"', .., (byte)'"'] or [(byte)'\'', .., (byte)'\'']
            ? span[1..^1]
            : span;

    /// <summary>True when <paramref name="line"/> is a top-level mapping key — no leading whitespace, contains <c>:</c>, not a comment / list marker / frontmatter delimiter.</summary>
    /// <param name="line">Single line of YAML, including any trailing newline.</param>
    /// <returns>True for top-level mapping keys.</returns>
    public static bool IsTopLevelKey(ReadOnlySpan<byte> line)
    {
        if (line.IsEmpty)
        {
            return false;
        }

        var first = line[0];
        if (first is (byte)' ' or (byte)'\t' or (byte)'#' or (byte)'-' or (byte)'\n' or (byte)'\r')
        {
            return false;
        }

        if (line.StartsWith("---"u8) || line.StartsWith("..."u8))
        {
            return false;
        }

        var colon = line.IndexOf((byte)':');
        return colon > 0;
    }

    /// <summary>Returns the byte slice covering the key text only — up to but excluding the colon, with trailing inline whitespace dropped.</summary>
    /// <param name="line">Single line of YAML.</param>
    /// <returns>Key span; empty when no colon is present.</returns>
    public static ReadOnlySpan<byte> KeyOf(ReadOnlySpan<byte> line)
    {
        var colon = line.IndexOf((byte)':');
        if (colon < 0)
        {
            return [];
        }

        var key = line[..colon];
        var p = key.Length;
        while (p > 0 && InlineWhitespace.Contains(key[p - 1]))
        {
            p--;
        }

        return key[..p];
    }

    /// <summary>Advances past the value rows of a YAML key that started on the previous line — indented continuations and list rows are part of the value; we stop at the next top-level key.</summary>
    /// <param name="source">Source YAML.</param>
    /// <param name="cursor">Cursor at the start of the line *after* the key line.</param>
    /// <returns>Cursor at the start of the next top-level key (or source-end).</returns>
    public static int AdvancePastValue(ReadOnlySpan<byte> source, int cursor)
    {
        while (cursor < source.Length)
        {
            var lineEnd = LineEnd(source, cursor);
            var line = source[cursor..lineEnd];
            if (line.IsEmpty)
            {
                break;
            }

            var first = line[0];
            if (first is (byte)' ' or (byte)'\t' or (byte)'-' or (byte)'#' or (byte)'\r' or (byte)'\n')
            {
                cursor = lineEnd;
                continue;
            }

            break;
        }

        return cursor;
    }
}
