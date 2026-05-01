// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Snippets;

/// <summary>
/// Byte-level extractor for snippet section markers
/// (<c>&lt;!-- @section name --&gt;</c> ... <c>&lt;!-- @endsection --&gt;</c>).
/// Operates on raw UTF-8 bytes; the caller passes the section name as a
/// span so no <see cref="string"/> is allocated for the comparison.
/// </summary>
/// <remarks>
/// HTML comments were chosen over a sigil-based marker (e.g. pymdownx's
/// <c>--;--</c>) because they are invisible to every CommonMark renderer
/// even when the snippets plugin is not enabled — stripping the plugin
/// from the build doesn't leave dangling marker bytes in the rendered
/// HTML.
/// </remarks>
internal static class SnippetSectionExtractor
{
    /// <summary>Length of the trailing <c>\r\n</c> line terminator stripped from a marker line.</summary>
    private const int CrLfLength = 2;

    /// <summary>Gets the UTF-8 prefix common to a section open marker.</summary>
    private static ReadOnlySpan<byte> SectionOpenPrefix => "<!-- @section "u8;

    /// <summary>Gets the UTF-8 trailing close of a section marker comment.</summary>
    private static ReadOnlySpan<byte> CommentClose => " -->"u8;

    /// <summary>Gets the UTF-8 marker that ends the active section.</summary>
    private static ReadOnlySpan<byte> SectionEndMarker => "<!-- @endsection -->"u8;

    /// <summary>Locates the <paramref name="section"/> block in <paramref name="source"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="section">Section name (raw UTF-8 bytes; matched verbatim — no case folding).</param>
    /// <param name="bodyStart">Byte offset where the section body begins (immediately after the open marker line).</param>
    /// <param name="bodyLength">Byte length of the section body (excluding the end marker line).</param>
    /// <returns>True when the section was found.</returns>
    public static bool TryFind(ReadOnlySpan<byte> source, ReadOnlySpan<byte> section, out int bodyStart, out int bodyLength)
    {
        bodyStart = 0;
        bodyLength = 0;

        if (!TryFindOpen(source, section, out var openLineEnd))
        {
            return false;
        }

        bodyStart = openLineEnd;
        var rest = source[bodyStart..];
        var endRel = rest.IndexOf(SectionEndMarker);
        if (endRel < 0)
        {
            return false;
        }

        // Trim back to the start of the line that hosts the end marker so we
        // don't include a leading newline left over from the body.
        var endLineStart = TrimToLineStart(source, bodyStart + endRel);
        bodyLength = endLineStart - bodyStart;
        return true;
    }

    /// <summary>Walks <paramref name="source"/> looking for the open marker for <paramref name="section"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="section">Section name bytes.</param>
    /// <param name="lineEnd">Byte offset just past the newline that terminates the marker line.</param>
    /// <returns>True when an open marker for this section is found.</returns>
    private static bool TryFindOpen(ReadOnlySpan<byte> source, ReadOnlySpan<byte> section, out int lineEnd)
    {
        lineEnd = 0;
        var cursor = 0;
        while (cursor < source.Length)
        {
            if (!MarkdownCodeScanner.AtLineStart(source, cursor))
            {
                cursor++;
                continue;
            }

            var thisLineEnd = MarkdownCodeScanner.LineEnd(source, cursor);
            var line = source[cursor..thisLineEnd];
            if (LineMatchesOpen(line, section))
            {
                lineEnd = thisLineEnd;
                return true;
            }

            cursor = thisLineEnd;
        }

        return false;
    }

    /// <summary>Returns true when <paramref name="line"/> is exactly <c>&lt;!-- @section &lt;name&gt; --&gt;</c> for <paramref name="section"/>.</summary>
    /// <param name="line">UTF-8 line bytes (may include a trailing newline).</param>
    /// <param name="section">Section name bytes.</param>
    /// <returns>True on a verbatim match.</returns>
    private static bool LineMatchesOpen(ReadOnlySpan<byte> line, ReadOnlySpan<byte> section)
    {
        var trimmed = StripTrailingNewline(line);
        if (!trimmed.StartsWith(SectionOpenPrefix))
        {
            return false;
        }

        var afterPrefix = trimmed[SectionOpenPrefix.Length..];
        if (!afterPrefix.EndsWith(CommentClose))
        {
            return false;
        }

        var name = afterPrefix[..^CommentClose.Length];
        return name.SequenceEqual(section);
    }

    /// <summary>Strips a single trailing <c>\r\n</c> or <c>\n</c> from <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>Slice without the line terminator.</returns>
    private static ReadOnlySpan<byte> StripTrailingNewline(ReadOnlySpan<byte> line)
    {
        if (line is [.., (byte)'\r', (byte)'\n'])
        {
            return line[..^CrLfLength];
        }

        if (line is [.., (byte)'\n'])
        {
            return line[..^1];
        }

        return line;
    }

    /// <summary>Walks back from <paramref name="offset"/> to the start of the line that contains it.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Absolute offset within <paramref name="source"/>.</param>
    /// <returns>Offset of the first byte of the hosting line.</returns>
    private static int TrimToLineStart(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p > 0 && source[p - 1] is not (byte)'\n')
        {
            p--;
        }

        return p;
    }
}
