// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Builders;

/// <summary>Splits a UTF-8 byte span on ASCII space / tab into per-token byte arrays.</summary>
/// <remarks>
/// Used by <see cref="ByteKeywordSet"/> and <see cref="OperatorAlternationFactory"/> so a language
/// can author its keyword / operator tables as a single space-delimited UTF-8 literal instead of
/// per-line <c>[.. "x"u8]</c> entries that the duplicate-line detector flags across every file.
/// </remarks>
internal static class WhitespaceSplitter
{
    /// <summary>ASCII space + tab dispatch set.</summary>
    private static readonly SearchValues<byte> SpaceOrTab = SearchValues.Create(" \t"u8);

    /// <summary>Splits <paramref name="source"/> on ASCII space / tab, skipping empty runs.</summary>
    /// <param name="source">Source bytes.</param>
    /// <returns>Per-token byte arrays; empty when <paramref name="source"/> contains no non-whitespace bytes.</returns>
    public static byte[][] Split(ReadOnlySpan<byte> source)
    {
        var tokenCount = CountTokens(source);
        if (tokenCount is 0)
        {
            return [];
        }

        var result = new byte[tokenCount][];
        var idx = 0;
        var rest = source;
        while (TryNextToken(ref rest, out var token))
        {
            result[idx++] = token.ToArray();
        }

        return result;
    }

    /// <summary>Counts the number of non-empty tokens in <paramref name="source"/>.</summary>
    /// <param name="source">Source bytes.</param>
    /// <returns>Token count.</returns>
    private static int CountTokens(ReadOnlySpan<byte> source)
    {
        var count = 0;
        var rest = source;
        while (TryNextToken(ref rest, out _))
        {
            count++;
        }

        return count;
    }

    /// <summary>Advances <paramref name="rest"/> past leading whitespace and yields the next non-empty token.</summary>
    /// <param name="rest">Remaining bytes; updated in place to point past the yielded token.</param>
    /// <param name="token">The yielded token (empty when no more tokens).</param>
    /// <returns>True when a token was produced; false at end of input.</returns>
    private static bool TryNextToken(ref ReadOnlySpan<byte> rest, out ReadOnlySpan<byte> token)
    {
        var start = rest.IndexOfAnyExcept(SpaceOrTab);
        if (start < 0)
        {
            token = default;
            rest = default;
            return false;
        }

        var trimmed = rest[start..];
        var end = trimmed.IndexOfAny(SpaceOrTab);
        if (end < 0)
        {
            token = trimmed;
            rest = default;
            return true;
        }

        token = trimmed[..end];
        rest = trimmed[end..];
        return true;
    }
}
