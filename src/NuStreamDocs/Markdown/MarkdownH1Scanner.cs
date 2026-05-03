// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Yaml;

namespace NuStreamDocs.Markdown;

/// <summary>
/// UTF-8 scanner for the first H1 heading in a markdown document.
/// </summary>
/// <remarks>
/// Recognizes both ATX (<c># Heading</c>) and Setext (<c>Heading</c> on one line, <c>===</c> underline on the next)
/// per CommonMark. Skips fenced code blocks (<c>```</c> / <c>~~~</c>) so a heading-shaped line inside a code fence
/// doesn't win, and skips a leading YAML front-matter block.
/// </remarks>
public static class MarkdownH1Scanner
{
    /// <summary>Maximum leading-space indent CommonMark allows before a heading marker.</summary>
    private const int MaxCommonMarkIndent = 3;

    /// <summary>Minimum run length for a code-fence marker.</summary>
    private const int MinFenceRun = 3;

    /// <summary>Returns the inline-text bytes of the first H1 in <paramref name="source"/>, or empty when none.</summary>
    /// <param name="source">UTF-8 markdown bytes (may include front-matter).</param>
    /// <returns>Slice into <paramref name="source"/> with the H1 text; empty when no H1 is present.</returns>
    public static ReadOnlySpan<byte> FindFirst(ReadOnlySpan<byte> source)
    {
        var cursor = YamlByteScanner.TryFindFrontmatter(source, out _, out var afterCloser)
            ? afterCloser
            : 0;

        ReadOnlySpan<byte> fenceMarker = default;
        ReadOnlySpan<byte> previousLine = default;
        while (cursor < source.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(source, cursor);
            var line = source[cursor..lineEnd];
            var trimmedLeading = TrimUpToThreeLeadingSpaces(line);

            if (TryAdvanceFence(trimmedLeading, ref fenceMarker))
            {
                previousLine = default;
                cursor = lineEnd;
                continue;
            }

            if (TryMatchHeading(trimmedLeading, previousLine, out var heading))
            {
                return heading;
            }

            previousLine = YamlByteScanner.TrimWhitespace(line).IsEmpty
                ? default
                : line;
            cursor = lineEnd;
        }

        return [];
    }

    /// <summary>Advances the fence state — opens a fence on a marker run, closes it on a matching closer.</summary>
    /// <param name="trimmedLeading">Line bytes with leading indent already stripped.</param>
    /// <param name="fenceMarker">Active fence marker; empty when not inside a fence. Mutated in place.</param>
    /// <returns>True when <paramref name="trimmedLeading"/> was consumed as a fence boundary or a body line inside an active fence.</returns>
    private static bool TryAdvanceFence(ReadOnlySpan<byte> trimmedLeading, ref ReadOnlySpan<byte> fenceMarker)
    {
        if (fenceMarker.IsEmpty)
        {
            if (!TryGetFenceMarker(trimmedLeading, out var marker))
            {
                return false;
            }

            fenceMarker = marker;
            return true;
        }

        if (!IsFenceLine(trimmedLeading, fenceMarker))
        {
            return true;
        }

        fenceMarker = default;
        return true;
    }

    /// <summary>Returns the H1 heading text when <paramref name="trimmedLeading"/> is an ATX H1 or a Setext underline backing <paramref name="previousLine"/>.</summary>
    /// <param name="trimmedLeading">Line bytes with leading indent already stripped.</param>
    /// <param name="previousLine">The non-empty line preceding <paramref name="trimmedLeading"/>; empty when the previous line was blank.</param>
    /// <param name="heading">Heading text bytes; empty unless the method returns true.</param>
    /// <returns>True when an H1 was matched.</returns>
    private static bool TryMatchHeading(ReadOnlySpan<byte> trimmedLeading, ReadOnlySpan<byte> previousLine, out ReadOnlySpan<byte> heading)
    {
        if (IsAtxH1(trimmedLeading, out var atxText))
        {
            heading = atxText;
            return true;
        }

        if (!previousLine.IsEmpty && IsSetextH1Underline(trimmedLeading))
        {
            heading = YamlByteScanner.TrimWhitespace(previousLine);
            return true;
        }

        heading = default;
        return false;
    }

    /// <summary>Trims up to three leading space bytes from <paramref name="line"/>; CommonMark allows that much indent before a heading.</summary>
    /// <param name="line">Source line bytes.</param>
    /// <returns>The line with up to three leading spaces removed.</returns>
    private static ReadOnlySpan<byte> TrimUpToThreeLeadingSpaces(ReadOnlySpan<byte> line)
    {
        var i = 0;
        while (i < MaxCommonMarkIndent && i < line.Length && line[i] is (byte)' ')
        {
            i++;
        }

        return line[i..];
    }

    /// <summary>Returns true when <paramref name="line"/> is an ATX H1 (single <c>#</c> followed by whitespace + content), and emits the inline text.</summary>
    /// <param name="line">Line bytes with leading indent already stripped.</param>
    /// <param name="text">Inline H1 text bytes; empty unless the method returns true.</param>
    /// <returns>True for an ATX H1 line.</returns>
    private static bool IsAtxH1(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> text)
    {
        text = [];
        if (line is not [(byte)'#', ..] || line.Length < 2 || line[1] is (byte)'#')
        {
            return false;
        }

        if (line[1] is not ((byte)' ' or (byte)'\t'))
        {
            return false;
        }

        var rest = YamlByteScanner.TrimWhitespace(line[2..]);
        rest = StripTrailingAtxClosingHashes(rest);
        if (rest.IsEmpty)
        {
            return false;
        }

        text = rest;
        return true;
    }

    /// <summary>Drops the optional trailing run of <c>#</c> bytes from an ATX heading per CommonMark.</summary>
    /// <param name="text">Trimmed heading text.</param>
    /// <returns>The text with any trailing <c>#</c>s and the preceding whitespace removed.</returns>
    private static ReadOnlySpan<byte> StripTrailingAtxClosingHashes(ReadOnlySpan<byte> text)
    {
        var end = text.Length;
        while (end > 0 && text[end - 1] is (byte)'#')
        {
            end--;
        }

        if (end == text.Length)
        {
            return text;
        }

        if (end > 0 && text[end - 1] is not ((byte)' ' or (byte)'\t'))
        {
            // Hashes were attached to a word — keep them (e.g. "C#").
            return text;
        }

        return YamlByteScanner.TrimWhitespace(text[..end]);
    }

    /// <summary>Returns true when <paramref name="line"/> is a Setext H1 underline (<c>=</c> bytes only, optional trailing whitespace).</summary>
    /// <param name="line">Line bytes with leading indent already stripped.</param>
    /// <returns>True for a Setext H1 underline.</returns>
    private static bool IsSetextH1Underline(ReadOnlySpan<byte> line)
    {
        if (line.IsEmpty || line[0] is not (byte)'=')
        {
            return false;
        }

        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] is (byte)'=')
            {
                continue;
            }

            if (line[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>Returns true when <paramref name="line"/> opens a fenced code block, emitting the marker run for matching the closer.</summary>
    /// <param name="line">Line bytes with leading indent already stripped.</param>
    /// <param name="marker">Fence marker bytes (<c>```</c> or <c>~~~</c> repeated); empty unless the method returns true.</param>
    /// <returns>True for a fence-open line.</returns>
    private static bool TryGetFenceMarker(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> marker)
    {
        marker = [];
        if (line.Length < MinFenceRun)
        {
            return false;
        }

        var ch = line[0];
        if (ch is not ((byte)'`' or (byte)'~'))
        {
            return false;
        }

        var run = 0;
        while (run < line.Length && line[run] == ch)
        {
            run++;
        }

        if (run < MinFenceRun)
        {
            return false;
        }

        marker = line[..run];
        return true;
    }

    /// <summary>Returns true when <paramref name="line"/> closes the active fence — same character and length ≥ the opener.</summary>
    /// <param name="line">Line bytes with leading indent already stripped.</param>
    /// <param name="opener">Marker bytes from the matching open fence.</param>
    /// <returns>True when this line is a closing fence.</returns>
    private static bool IsFenceLine(ReadOnlySpan<byte> line, ReadOnlySpan<byte> opener)
    {
        if (opener.IsEmpty || line.Length < opener.Length)
        {
            return false;
        }

        var ch = opener[0];
        var run = 0;
        while (run < line.Length && line[run] == ch)
        {
            run++;
        }

        if (run < opener.Length)
        {
            return false;
        }

        var rest = YamlByteScanner.TrimWhitespace(line[run..]);
        return rest.IsEmpty;
    }
}
