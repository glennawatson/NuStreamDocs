// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.Abbr;

/// <summary>Folds <c>*[ABBR]: definition</c> lines into <c>&lt;abbr title="..."&gt;</c> wrappers around occurrences in the body.</summary>
internal static class AbbrRewriter
{
    /// <summary>Length of the <c>"]:"</c> separator between an abbreviation token and its definition body.</summary>
    private const int CloseBracketColonLength = 2;

    /// <summary>Gets the two-byte prefix that introduces an abbreviation definition.</summary>
    private static ReadOnlySpan<byte> DefinitionMarker => "*["u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        Dictionary<string, byte[]> definitions = new(StringComparer.Ordinal);
        var stripped = CollectAndStripDefinitions(source, definitions);
        if (definitions.Count is 0)
        {
            writer.Write(stripped);
            return;
        }

        WrapAbbreviations(stripped, definitions, writer);
    }

    /// <summary>Walks <paramref name="source"/>, captures every definition into <paramref name="definitions"/>, and returns the body with those lines removed.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="definitions">Accumulator: token → title bytes.</param>
    /// <returns>Source bytes with definition lines stripped.</returns>
    private static byte[] CollectAndStripDefinitions(ReadOnlySpan<byte> source, Dictionary<string, byte[]> definitions)
    {
        ArrayBufferWriter<byte> sink = new(source.Length);
        var cursor = 0;
        while (cursor < source.Length)
        {
            var lineEnd = Utf8LineSpan.FindLineEnd(source, cursor);
            var line = source[cursor..lineEnd];
            if (TryParseDefinition(line, out var token, out var title))
            {
                definitions[Encoding.UTF8.GetString(token)] = title.ToArray();
                cursor = Utf8LineSpan.AdvancePastLineTerminator(source, lineEnd);
                continue;
            }

            var nextCursor = Utf8LineSpan.AdvancePastLineTerminator(source, lineEnd);
            sink.Write(source[cursor..nextCursor]);
            cursor = nextCursor;
        }

        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Recognises <c>*[TOKEN]: definition</c> on a single line.</summary>
    /// <param name="line">Source line, no terminator.</param>
    /// <param name="token">Captured token bytes (between <c>[</c> and <c>]</c>) on success.</param>
    /// <param name="title">Captured title bytes (everything after <c>:</c> with leading whitespace trimmed) on success.</param>
    /// <returns>True when the line is a definition.</returns>
    private static bool TryParseDefinition(
        ReadOnlySpan<byte> line,
        out ReadOnlySpan<byte> token,
        out ReadOnlySpan<byte> title)
    {
        token = default;
        title = default;
        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(line);
        if (!trimmed.StartsWith(DefinitionMarker))
        {
            return false;
        }

        var rest = trimmed[DefinitionMarker.Length..];
        var closeBracket = rest.IndexOf((byte)']');
        if (closeBracket <= 0 || closeBracket + 1 >= rest.Length || rest[closeBracket + 1] is not (byte)':')
        {
            return false;
        }

        token = rest[..closeBracket];
        title = AsciiByteHelpers.TrimAsciiWhitespace(rest[(closeBracket + CloseBracketColonLength)..]);
        return title.Length > 0;
    }

    /// <summary>Walks the body bytes and wraps each definition-token occurrence in <c>&lt;abbr title="…"&gt;</c> elements.</summary>
    /// <param name="source">UTF-8 source bytes (definitions already stripped).</param>
    /// <param name="definitions">Token → title lookup.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WrapAbbreviations(
        ReadOnlySpan<byte> source,
        Dictionary<string, byte[]> definitions,
        IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (TrySkipCodeOrLink(source, i, writer, out var afterSkip))
            {
                i = afterSkip;
                continue;
            }

            if (TryEmitAbbrToken(source, i, definitions, writer, out var afterToken))
            {
                i = afterToken;
                continue;
            }

            var dest = writer.GetSpan(1);
            dest[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Consumes any verbatim-skip span at <paramref name="offset"/> (fenced/inline code, markdown link, markdown image) into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="afterSkip">Cursor position past the consumed span on success.</param>
    /// <returns>True when bytes were consumed verbatim.</returns>
    private static bool TrySkipCodeOrLink(
        ReadOnlySpan<byte> source,
        int offset,
        IBufferWriter<byte> writer,
        out int afterSkip)
    {
        afterSkip = offset;
        var b = source[offset];
        if (b is (byte)'`')
        {
            var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, offset);
            writer.Write(source[offset..inlineEnd]);
            afterSkip = inlineEnd;
            return true;
        }

        if (MarkdownCodeScanner.AtLineStart(source, offset)
            && MarkdownCodeScanner.TryConsumeFence(source, offset, out var fenceEnd))
        {
            writer.Write(source[offset..fenceEnd]);
            afterSkip = fenceEnd;
            return true;
        }

        if (b is not (byte)'[' && !(b is (byte)'!' && offset + 1 < source.Length && source[offset + 1] is (byte)'['))
        {
            return false;
        }

        var linkEnd = ConsumeMarkdownLinkSpan(source, offset);
        if (linkEnd <= offset)
        {
            return false;
        }

        writer.Write(source[offset..linkEnd]);
        afterSkip = linkEnd;
        return true;
    }

    /// <summary>Tries to wrap a defined abbreviation token at <paramref name="offset"/> in <c>&lt;abbr&gt;</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="definitions">Token → title lookup.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="afterToken">Cursor position past the consumed token on success.</param>
    /// <returns>True when a token was emitted.</returns>
    private static bool TryEmitAbbrToken(
        ReadOnlySpan<byte> source,
        int offset,
        Dictionary<string, byte[]> definitions,
        IBufferWriter<byte> writer,
        out int afterToken)
    {
        afterToken = offset;
        if (!TryMatchToken(source, offset, definitions, out var tokenLength, out var title)
            || !AsciiWordBoundary.IsBefore(source, offset)
            || !AsciiWordBoundary.IsAfter(source, offset + tokenLength))
        {
            return false;
        }

        writer.Write("<abbr title=\""u8);
        writer.Write(title);
        writer.Write("\">"u8);
        writer.Write(source.Slice(offset, tokenLength));
        writer.Write("</abbr>"u8);
        afterToken = offset + tokenLength;
        return true;
    }

    /// <summary>Consumes a markdown link or image span at <paramref name="offset"/> verbatim, returning the end offset.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Offset of <c>[</c> (link) or <c>!</c> (image opener).</param>
    /// <returns>Exclusive end of the consumed span, or <paramref name="offset"/> when the bytes don't form a complete link/image.</returns>
    private static int ConsumeMarkdownLinkSpan(ReadOnlySpan<byte> source, int offset)
    {
        var bracketStart = source[offset] is (byte)'!' ? offset + 1 : offset;
        if (bracketStart >= source.Length || source[bracketStart] is not (byte)'[')
        {
            return offset;
        }

        var labelEnd = FindMatching(source, bracketStart + 1, (byte)'[', (byte)']');
        if (labelEnd < 0 || labelEnd + 1 >= source.Length || source[labelEnd + 1] is not (byte)'(')
        {
            return offset;
        }

        var destEnd = FindMatching(source, labelEnd + 2, (byte)'(', (byte)')');
        return destEnd < 0 ? offset : destEnd + 1;
    }

    /// <summary>Returns the index of the matching <paramref name="close"/> for a depth-1 opener; -1 when unbalanced.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="searchFrom">First byte to consider (one past the opener).</param>
    /// <param name="open">Opener byte.</param>
    /// <param name="close">Closer byte.</param>
    /// <returns>Index of the matching closer, or -1.</returns>
    private static int FindMatching(ReadOnlySpan<byte> source, int searchFrom, byte open, byte close)
    {
        var depth = 1;
        for (var i = searchFrom; i < source.Length; i++)
        {
            var b = source[i];
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
            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Tries to match any defined token at <paramref name="offset"/>; returns the longest match (greedy).</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="definitions">Token → title lookup.</param>
    /// <param name="tokenLength">Matched token length on success.</param>
    /// <param name="title">Title bytes for the matched token on success.</param>
    /// <returns>True when a defined token starts at <paramref name="offset"/>.</returns>
    private static bool TryMatchToken(
        ReadOnlySpan<byte> source,
        int offset,
        Dictionary<string, byte[]> definitions,
        out int tokenLength,
        out ReadOnlySpan<byte> title)
    {
        tokenLength = 0;
        title = default;
        var bestLen = 0;
        byte[]? bestTitle = null;
        foreach (var (key, value) in definitions)
        {
            if (key.Length <= bestLen)
            {
                continue;
            }

            if (offset + key.Length > source.Length)
            {
                continue;
            }

            var slice = source.Slice(offset, key.Length);
            if (!MatchesAscii(slice, key))
            {
                continue;
            }

            bestLen = key.Length;
            bestTitle = value;
        }

        if (bestTitle is null)
        {
            return false;
        }

        tokenLength = bestLen;
        title = bestTitle;
        return true;
    }

    /// <summary>Byte-equality check between <paramref name="slice"/> and the ASCII bytes of <paramref name="key"/>.</summary>
    /// <param name="slice">Source bytes (caller-sized to <paramref name="key"/>.Length).</param>
    /// <param name="key">Token string (assumed ASCII; abbreviations universally are).</param>
    /// <returns>True on byte-for-byte match.</returns>
    private static bool MatchesAscii(ReadOnlySpan<byte> slice, string key)
    {
        for (var i = 0; i < key.Length; i++)
        {
            if (slice[i] != (byte)key[i])
            {
                return false;
            }
        }

        return true;
    }
}
