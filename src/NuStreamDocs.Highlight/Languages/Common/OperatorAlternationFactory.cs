// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Helpers for assembling operator-alternation tables from a single UTF-8 byte literal.</summary>
/// <remarks>
/// Lexer operator tables must be ordered longest-first so the matcher prefers <c>+=</c> over <c>+</c>.
/// Authoring them as a per-line <c>[.. "+="u8]</c>, <c>[.. "+"u8]</c>, … literal is verbose and trips the
/// duplicate-line detector across every language file. This factory accepts a single space-delimited
/// UTF-8 literal and returns a <c>byte[][]</c> already sorted by descending length, plus a helper to
/// derive the corresponding first-byte dispatch set.
/// </remarks>
internal static class OperatorAlternationFactory
{
    /// <summary>Splits a space-delimited UTF-8 byte literal into a longest-first <c>byte[][]</c> alternation table.</summary>
    /// <param name="spaceSeparated">Whitespace-delimited UTF-8 operator bytes (e.g. <c>"+= -= + - *"u8</c>).</param>
    /// <returns>Operator byte arrays sorted by descending length, ready for the lexer's operator rule.</returns>
    /// <remarks>
    /// Empty runs (consecutive whitespace) are skipped silently; only ASCII space (<c>0x20</c>) and tab (<c>0x09</c>) act as separators.
    /// Within equal-length groups the original input order is preserved.
    /// </remarks>
    public static byte[][] SplitLongestFirst(ReadOnlySpan<byte> spaceSeparated) =>
        SortLongestFirst(SplitSpaceSeparated(spaceSeparated));

    /// <summary>Splits two space-delimited UTF-8 byte literals into a single longest-first <c>byte[][]</c> alternation table.</summary>
    /// <param name="spaceSeparatedFirst">First whitespace-delimited UTF-8 operator chunk.</param>
    /// <param name="spaceSeparatedSecond">Second whitespace-delimited UTF-8 operator chunk.</param>
    /// <returns>Operator byte arrays sorted by descending length.</returns>
    public static byte[][] SplitLongestFirst(ReadOnlySpan<byte> spaceSeparatedFirst, ReadOnlySpan<byte> spaceSeparatedSecond)
    {
        var first = WhitespaceSplitter.Split(spaceSeparatedFirst);
        var second = WhitespaceSplitter.Split(spaceSeparatedSecond);
        if (second.Length is 0)
        {
            return SortLongestFirst(first);
        }

        if (first.Length is 0)
        {
            return SortLongestFirst(second);
        }

        var combined = new byte[first.Length + second.Length][];
        Array.Copy(first, 0, combined, 0, first.Length);
        Array.Copy(second, 0, combined, first.Length, second.Length);
        return SortLongestFirst(combined);
    }

    /// <summary>Builds a <see cref="SearchValues{T}"/> covering every alternation entry's leading byte.</summary>
    /// <param name="operators">Operator byte arrays (typically the result of <see cref="SplitLongestFirst(ReadOnlySpan{byte})"/>).</param>
    /// <returns>First-byte dispatch set.</returns>
    public static SearchValues<byte> FirstBytesOf(byte[][] operators)
    {
        ArgumentNullException.ThrowIfNull(operators);
        const int AsciiByteCount = 256;
        Span<bool> seen = stackalloc bool[AsciiByteCount];
        for (var i = 0; i < operators.Length; i++)
        {
            var op = operators[i];
            if (op is null or [])
            {
                throw new ArgumentException("Operator entries must be non-null and non-empty.", nameof(operators));
            }

            seen[op[0]] = true;
        }

        var distinctCount = 0;
        for (var b = 0; b < seen.Length; b++)
        {
            if (seen[b])
            {
                distinctCount++;
            }
        }

        var result = new byte[distinctCount];
        var idx = 0;
        for (var b = 0; b < seen.Length; b++)
        {
            if (seen[b])
            {
                result[idx++] = (byte)b;
            }
        }

        return SearchValues.Create(result);
    }

    /// <summary>Splits a UTF-8 byte span on ASCII space / tab, skipping empty runs.</summary>
    /// <param name="source">Source bytes.</param>
    /// <returns>Per-token byte arrays.</returns>
    private static byte[][] SplitSpaceSeparated(ReadOnlySpan<byte> source) =>
        WhitespaceSplitter.Split(source);

    /// <summary>Stable counting-sort by descending token length.</summary>
    /// <param name="tokens">Token byte arrays.</param>
    /// <returns>Same tokens reordered so longest entries come first; ties preserve input order.</returns>
    private static byte[][] SortLongestFirst(byte[][] tokens)
    {
        if (tokens.Length is 0)
        {
            return tokens;
        }

        // Stable counting sort by length (descending). Operator tokens are short
        // (1-3 bytes typical, ~4 max), so a single pass over a small bucket table
        // is cheaper than introsort and preserves intra-length input order.
        var maxLen = 0;
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Length > maxLen)
            {
                maxLen = tokens[i].Length;
            }
        }

        var result = new byte[tokens.Length][];
        var cursor = 0;
        for (var len = maxLen; len >= 1; len--)
        {
            for (var i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Length == len)
                {
                    result[cursor++] = tokens[i];
                }
            }
        }

        return result;
    }
}
