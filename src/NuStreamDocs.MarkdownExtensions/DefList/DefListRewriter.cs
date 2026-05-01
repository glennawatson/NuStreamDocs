// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.DefList;

/// <summary>
/// Stateless UTF-8 definition-list rewriter. Pairs every
/// non-blank line followed by one or more <c>: definition</c> lines
/// into a <c>&lt;dl&gt;</c> block.
/// </summary>
internal static class DefListRewriter
{
    /// <summary>Length of the <c>": "</c> definition prefix.</summary>
    private const int DefinitionPrefixLength = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i)
                && TryParseGroup(source, i, out var groupEnd))
            {
                EmitGroup(source[i..groupEnd], writer);
                i = groupEnd;
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, i);
            writer.Write(source[i..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Tries to detect a definition-list group (term + <c>:</c> definitions) at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Candidate line-start offset.</param>
    /// <param name="groupEnd">Set to the exclusive end of the group on success.</param>
    /// <returns>True when at least one term/definition pair was found.</returns>
    private static bool TryParseGroup(ReadOnlySpan<byte> source, int offset, out int groupEnd)
    {
        groupEnd = 0;
        var p = offset;
        var foundPair = false;
        while (p < source.Length)
        {
            // Term line — non-blank, non-": ", not already the start of a definition.
            var termEnd = MarkdownCodeScanner.LineEnd(source, p);
            if (IsBlankSlice(source[p..termEnd]) || IsDefinitionLine(source, p))
            {
                break;
            }

            // The line after the term must be a ": " definition.
            if (!IsDefinitionLine(source, termEnd))
            {
                break;
            }

            // Consume one or more definition lines.
            var afterDefs = termEnd;
            while (afterDefs < source.Length && IsDefinitionLine(source, afterDefs))
            {
                afterDefs = MarkdownCodeScanner.LineEnd(source, afterDefs);
            }

            foundPair = true;
            p = afterDefs;
        }

        if (!foundPair)
        {
            return false;
        }

        groupEnd = p;
        return true;
    }

    /// <summary>Returns true when the line at <paramref name="offset"/> begins with <c>": "</c>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <returns>True when the line is a definition.</returns>
    private static bool IsDefinitionLine(ReadOnlySpan<byte> source, int offset) =>
        offset + 1 < source.Length
            && source[offset] == (byte)':'
            && source[offset + 1] == (byte)' ';

    /// <summary>Returns true when <paramref name="line"/> is blank.</summary>
    /// <param name="line">UTF-8 line bytes including any trailing newline.</param>
    /// <returns>True when blank.</returns>
    private static bool IsBlankSlice(ReadOnlySpan<byte> line) => IndentedBlockScanner.IsBlankLine(line);

    /// <summary>Emits the <c>&lt;dl&gt;</c> block for one parsed group.</summary>
    /// <param name="group">UTF-8 bytes of the group (term + definitions interleaved).</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitGroup(ReadOnlySpan<byte> group, IBufferWriter<byte> writer)
    {
        writer.Write("\n<dl>\n"u8);

        var i = 0;
        while (i < group.Length)
        {
            var lineEnd = MarkdownCodeScanner.LineEnd(group, i);
            if (IsDefinitionLine(group, i))
            {
                writer.Write("<dd>"u8);
                HtmlEscaper.Escape(TrimNewline(group[(i + DefinitionPrefixLength)..lineEnd]), writer);
                writer.Write("</dd>\n"u8);
            }
            else
            {
                writer.Write("<dt>"u8);
                HtmlEscaper.Escape(TrimNewline(group[i..lineEnd]), writer);
                writer.Write("</dt>\n"u8);
            }

            i = lineEnd;
        }

        writer.Write("</dl>\n\n"u8);
    }

    /// <summary>Strips a trailing CRLF or LF from <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>Slice without the line terminator.</returns>
    private static ReadOnlySpan<byte> TrimNewline(ReadOnlySpan<byte> line)
    {
        if (line.Length is 0)
        {
            return line;
        }

        if (line[^1] == (byte)'\n')
        {
            line = line[..^1];
        }

        if (line.Length > 0 && line[^1] == (byte)'\r')
        {
            line = line[..^1];
        }

        return line;
    }
}
