// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
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
        var defs = new Dictionary<string, string>(StringComparer.Ordinal);
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
    /// <param name="defs">Definition map populated in place.</param>
    /// <returns>UTF-8 byte array with <c>*[…]: …</c> lines stripped.</returns>
    private static byte[] StripDefinitions(ReadOnlySpan<byte> source, Dictionary<string, string> defs)
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
    /// <param name="token">Captured token on success.</param>
    /// <param name="definition">Captured definition (trimmed) on success.</param>
    /// <returns>True when <paramref name="line"/> matched.</returns>
    private static bool TryParseDefinition(ReadOnlySpan<byte> line, out string token, out string definition)
    {
        token = string.Empty;
        definition = string.Empty;
        const int MinDefinitionLength = 5; // "*[X]:" — the smallest legal opener
        var trimmed = TrimTrailingNewline(line);
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

        token = Encoding.UTF8.GetString(trimmed[DefinitionOpenerLength..bracketEnd]);
        definition = Encoding.UTF8.GetString(trimmed[(bracketEnd + DefinitionOpenerLength)..]).Trim();
        return token.Length > 0 && definition.Length > 0;
    }

    /// <summary>Walks <paramref name="source"/> and wraps every word-boundary occurrence of any <paramref name="tokens"/> entry into <c>&lt;abbr&gt;</c>.</summary>
    /// <param name="source">Source with definition lines already stripped.</param>
    /// <param name="tokens">Tokens sorted longest-first.</param>
    /// <param name="defs">Token-to-definition map.</param>
    /// <param name="writer">Sink.</param>
    private static void WrapOccurrences(ReadOnlySpan<byte> source, AbbrToken[] tokens, Dictionary<string, string> defs, IBufferWriter<byte> writer)
    {
        CodeAwareRewriter.Run(source, writer, TryWrap);

        bool TryWrap(ReadOnlySpan<byte> s, int offset, IBufferWriter<byte> w, out int consumed)
        {
            if (TryMatchAnyToken(s, offset, tokens, out var matched))
            {
                EmitAbbr(matched.Text, defs[matched.Text], w);
                consumed = matched.Bytes.Length;
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
    private static bool TryMatchAnyToken(ReadOnlySpan<byte> source, int offset, AbbrToken[] tokens, out AbbrToken matched)
    {
        matched = default;
        for (var t = 0; t < tokens.Length; t++)
        {
            if (!AsciiWordBoundary.TryMatchBounded(source, offset, tokens[t].Bytes))
            {
                continue;
            }

            matched = tokens[t];
            return true;
        }

        return false;
    }

    /// <summary>Builds the sorted abbreviation-token table with cached UTF-8 bytes.</summary>
    /// <param name="defs">Definition map.</param>
    /// <returns>Tokens sorted longest-first by UTF-8 byte length.</returns>
    private static AbbrToken[] BuildTokens(Dictionary<string, string> defs)
    {
        var tokens = new AbbrToken[defs.Count];
        var index = 0;
        using var enumerator = defs.Keys.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var text = enumerator.Current;
            tokens[index++] = new(text, Encoding.UTF8.GetBytes(text));
        }

        Array.Sort(tokens, static (a, b) => b.Bytes.Length.CompareTo(a.Bytes.Length));
        return tokens;
    }

    /// <summary>Writes the <c>&lt;abbr title="…"&gt;token&lt;/abbr&gt;</c> wrapper.</summary>
    /// <param name="token">Token that matched.</param>
    /// <param name="definition">Definition text.</param>
    /// <param name="writer">Sink.</param>
    private static void EmitAbbr(string token, string definition, IBufferWriter<byte> writer)
    {
        writer.Write("<abbr title=\""u8);
        WriteHtmlEscaped(writer, definition);
        writer.Write("\">"u8);
        Utf8StringWriter.Write(writer, token);
        writer.Write("</abbr>"u8);
    }

    /// <summary>Writes <paramref name="value"/> as UTF-8 with HTML attribute escapes for <c>"</c>, <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="value">Attribute value.</param>
    private static void WriteHtmlEscaped(IBufferWriter<byte> writer, string value)
    {
        var bytes = Encoding.UTF8.GetByteCount(value);
        var buffer = bytes <= 256 ? stackalloc byte[bytes] : new byte[bytes];
        Encoding.UTF8.GetBytes(value, buffer);
        XmlEntityEscaper.WriteEscaped(writer, buffer, XmlEntityEscaper.Mode.HtmlAttribute);
    }

    /// <summary>Strips a trailing <c>\n</c> (or <c>\r\n</c>) from <paramref name="line"/> if present.</summary>
    /// <param name="line">Candidate line.</param>
    /// <returns>Trimmed line.</returns>
    private static ReadOnlySpan<byte> TrimTrailingNewline(ReadOnlySpan<byte> line)
    {
        var end = line.Length;
        if (end > 0 && line[end - 1] is (byte)'\n')
        {
            end--;
        }

        if (end > 0 && line[end - 1] is (byte)'\r')
        {
            end--;
        }

        return line[..end];
    }

    /// <summary>Cached abbreviation token text plus its UTF-8 bytes.</summary>
    private readonly record struct AbbrToken(string Text, byte[] Bytes);
}
