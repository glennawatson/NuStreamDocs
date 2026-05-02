// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using static NuStreamDocs.Markdown.Common.MarkdownCodeScanner;

namespace NuStreamDocs.Abbr;

/// <summary>
/// Stateless UTF-8 abbreviation rewriter. Collects
/// <c>*[token]: definition</c> definition lines, strips them from
/// the output, then wraps every word-boundary occurrence of a
/// known token in <c>&lt;abbr&gt;</c>. Fenced and inline code are
/// passed through verbatim during the wrap phase.
/// </summary>
internal static class AbbrRewriter
{
    /// <summary>Length of the <c>*[</c> opener prefix on a definition line.</summary>
    private const int DefinitionOpenerLength = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var defs = new Dictionary<byte[], byte[]>(ByteArrayComparer.Instance);
        var stripped = StripDefinitions(source, defs);
        if (defs.Count is 0)
        {
            writer.Write(stripped);
            return;
        }

        // Sort tokens longest-first so a multi-word abbr beats a sub-word match.
        var tokens = BuildTokens(defs);

        WrapOccurrences(stripped, tokens, defs, writer);
    }

    /// <summary>Walks <paramref name="source"/>, recording any abbreviation definition lines into <paramref name="defs"/>, and returns the source with those lines removed.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="defs">Definition map populated in place; keyed on the UTF-8 token bytes, value is the UTF-8 trimmed definition body.</param>
    /// <returns>UTF-8 byte array with <c>*[…]: …</c> lines stripped.</returns>
    private static byte[] StripDefinitions(ReadOnlySpan<byte> source, Dictionary<byte[], byte[]> defs)
    {
        if (source.IsEmpty)
        {
            return [];
        }

        using var rental = PageBuilderPool.Rent(source.Length);
        var keep = rental.Writer;
        var i = 0;
        while (i < source.Length)
        {
            var lineStart = i;
            var lineEnd = LineEnd(source, i);

            if (TryParseDefinition(source[lineStart..lineEnd], out var token, out var definition))
            {
                defs[token] = definition;
                i = lineEnd;
                continue;
            }

            keep.Write(source[lineStart..lineEnd]);
            i = lineEnd;
        }

        return [.. keep.WrittenSpan];
    }

    /// <summary>Tries to parse <paramref name="line"/> as a <c>*[token]: definition</c> definition line.</summary>
    /// <param name="line">UTF-8 bytes of a single line including any trailing newline.</param>
    /// <param name="token">Captured UTF-8 token bytes on success.</param>
    /// <param name="definition">Captured UTF-8 definition bytes (trimmed) on success.</param>
    /// <returns>True when <paramref name="line"/> matched.</returns>
    private static bool TryParseDefinition(ReadOnlySpan<byte> line, out byte[] token, out byte[] definition)
    {
        token = [];
        definition = [];
        const int MinDefinitionLength = 5; // "*[X]:" — the smallest legal opener
        var trimmed = AsciiByteHelpers.TrimTrailingNewline(line);
        if (trimmed.Length < MinDefinitionLength || trimmed[0] is not (byte)'*' || trimmed[1] is not (byte)'[')
        {
            return false;
        }

        var closeBracket = trimmed[DefinitionOpenerLength..].IndexOf((byte)']');
        if (closeBracket <= 0)
        {
            return false;
        }

        var bracketEnd = DefinitionOpenerLength + closeBracket;
        if (bracketEnd + 1 >= trimmed.Length || trimmed[bracketEnd + 1] is not (byte)':')
        {
            return false;
        }

        var tokenSlice = trimmed[DefinitionOpenerLength..bracketEnd];
        var defSlice = AsciiByteHelpers.TrimAsciiWhitespace(trimmed[(bracketEnd + DefinitionOpenerLength)..]);
        if (tokenSlice.IsEmpty || defSlice.IsEmpty)
        {
            return false;
        }

        token = tokenSlice.ToArray();
        definition = defSlice.ToArray();
        return true;
    }

    /// <summary>Walks <paramref name="source"/> and wraps every word-boundary occurrence of any <paramref name="tokens"/> entry into <c>&lt;abbr&gt;</c>.</summary>
    /// <param name="source">Source with definition lines already stripped.</param>
    /// <param name="tokens">Tokens sorted longest-first.</param>
    /// <param name="defs">Token-bytes-to-definition-bytes map.</param>
    /// <param name="writer">Sink.</param>
    private static void WrapOccurrences(ReadOnlySpan<byte> source, byte[][] tokens, Dictionary<byte[], byte[]> defs, IBufferWriter<byte> writer)
    {
        CodeAwareRewriter.Run(source, writer, TryWrap);

        bool TryWrap(ReadOnlySpan<byte> s, int offset, IBufferWriter<byte> w, out int consumed)
        {
            if (TryMatchAnyToken(s, offset, tokens, out var matched))
            {
                EmitAbbr(matched, defs[matched], w);
                consumed = matched.Length;
                return true;
            }

            consumed = 0;
            return false;
        }
    }

    /// <summary>Tries each token (longest-first) against the source at <paramref name="offset"/>.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="tokens">Tokens sorted longest-first.</param>
    /// <param name="matched">The first matching token, when found.</param>
    /// <returns>True when a token matched at the current position with a trailing word boundary.</returns>
    private static bool TryMatchAnyToken(ReadOnlySpan<byte> source, int offset, byte[][] tokens, out byte[] matched)
    {
        matched = [];
        for (var t = 0; t < tokens.Length; t++)
        {
            if (!AsciiWordBoundary.TryMatchBounded(source, offset, tokens[t]))
            {
                continue;
            }

            matched = tokens[t];
            return true;
        }

        return false;
    }

    /// <summary>Builds the sorted token table from the keys of <paramref name="defs"/>.</summary>
    /// <param name="defs">Definition map.</param>
    /// <returns>Token byte arrays sorted longest-first.</returns>
    private static byte[][] BuildTokens(Dictionary<byte[], byte[]> defs)
    {
        var tokens = new byte[defs.Count][];
        var index = 0;
        using var enumerator = defs.Keys.GetEnumerator();
        while (enumerator.MoveNext())
        {
            tokens[index++] = enumerator.Current;
        }

        Array.Sort(tokens, static (a, b) => b.Length.CompareTo(a.Length));
        return tokens;
    }

    /// <summary>Writes the <c>&lt;abbr title="…"&gt;token&lt;/abbr&gt;</c> wrapper.</summary>
    /// <param name="token">Token bytes that matched.</param>
    /// <param name="definition">Definition bytes.</param>
    /// <param name="writer">Sink.</param>
    private static void EmitAbbr(ReadOnlySpan<byte> token, ReadOnlySpan<byte> definition, IBufferWriter<byte> writer)
    {
        writer.Write("<abbr title=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, definition, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\">"u8);
        writer.Write(token);
        writer.Write("</abbr>"u8);
    }
}
