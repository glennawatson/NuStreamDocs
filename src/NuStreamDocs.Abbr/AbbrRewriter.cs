// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
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
        var tokens = new string[defs.Count];
        defs.Keys.CopyTo(tokens, 0);
        Array.Sort(tokens, static (a, b) => b.Length.CompareTo(a.Length));

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

        var keep = new ArrayBufferWriter<byte>(source.Length);
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
    private static void WrapOccurrences(ReadOnlySpan<byte> source, string[] tokens, Dictionary<string, string> defs, IBufferWriter<byte> writer)
    {
        CodeAwareRewriter.Run(source, writer, TryWrap);

        bool TryWrap(ReadOnlySpan<byte> s, int offset, IBufferWriter<byte> w, out int consumed)
        {
            if (IsWordBoundaryBefore(s, offset) && TryMatchAnyToken(s, offset, tokens, out var matched))
            {
                EmitAbbr(matched, defs[matched], w);
                consumed = Encoding.UTF8.GetByteCount(matched);
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
    private static bool TryMatchAnyToken(ReadOnlySpan<byte> source, int offset, string[] tokens, out string matched)
    {
        matched = string.Empty;

        // Hoist one stack buffer outside the loop sized to the longest token's
        // UTF-8 byte length so per-iteration stack growth stays flat. Falls
        // back to the heap when any single token exceeds the safe stack budget.
        var maxTokenBytes = 0;
        for (var t = 0; t < tokens.Length; t++)
        {
            var n = Encoding.UTF8.GetByteCount(tokens[t]);
            if (n > maxTokenBytes)
            {
                maxTokenBytes = n;
            }
        }

        var probe = maxTokenBytes <= 256 ? stackalloc byte[256] : new byte[maxTokenBytes];
        for (var t = 0; t < tokens.Length; t++)
        {
            var tokenBytes = Encoding.UTF8.GetByteCount(tokens[t]);
            if (offset + tokenBytes > source.Length)
            {
                continue;
            }

            var slot = probe[..tokenBytes];
            Encoding.UTF8.GetBytes(tokens[t], slot);

            if (!source.Slice(offset, tokenBytes).SequenceEqual(slot))
            {
                continue;
            }

            if (!IsWordBoundaryAfter(source, offset + tokenBytes))
            {
                continue;
            }

            matched = tokens[t];
            return true;
        }

        return false;
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
        WriteUtf8(writer, token);
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

        var runStart = 0;
        for (var i = 0; i < buffer.Length; i++)
        {
            var entity = EntityFor(buffer[i]);
            if (entity.IsEmpty)
            {
                continue;
            }

            if (i > runStart)
            {
                writer.Write(buffer[runStart..i]);
            }

            writer.Write(entity);
            runStart = i + 1;
        }

        if (runStart >= buffer.Length)
        {
            return;
        }

        writer.Write(buffer[runStart..]);
    }

    /// <summary>Returns the HTML entity bytes for <paramref name="b"/>; an empty span when <paramref name="b"/> is plain.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>Entity bytes or empty.</returns>
    private static ReadOnlySpan<byte> EntityFor(byte b) => b switch
    {
        (byte)'"' => "&quot;"u8,
        (byte)'&' => "&amp;"u8,
        (byte)'<' => "&lt;"u8,
        (byte)'>' => "&gt;"u8,
        _ => default,
    };

    /// <summary>Encodes a UTF-16 string to UTF-8 in <paramref name="writer"/>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="value">Value.</param>
    private static void WriteUtf8(IBufferWriter<byte> writer, string value)
    {
        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var dst = writer.GetSpan(max);
        var written = Encoding.UTF8.GetBytes(value, dst);
        writer.Advance(written);
    }

    /// <summary>Returns true when <paramref name="offset"/> is at a word boundary on its left.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position to test.</param>
    /// <returns>True when boundary holds.</returns>
    private static bool IsWordBoundaryBefore(ReadOnlySpan<byte> source, int offset) =>
        offset is 0 || !IsWordByte(source[offset - 1]);

    /// <summary>Returns true when <paramref name="offset"/> is at a word boundary on its right.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position to test.</param>
    /// <returns>True when boundary holds.</returns>
    private static bool IsWordBoundaryAfter(ReadOnlySpan<byte> source, int offset) =>
        offset >= source.Length || !IsWordByte(source[offset]);

    /// <summary>Returns true when <paramref name="b"/> is an ASCII identifier byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True when classed as a word byte.</returns>
    private static bool IsWordByte(byte b) =>
        b is >= (byte)'0' and <= (byte)'9'
          or >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or (byte)'_';

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
}
