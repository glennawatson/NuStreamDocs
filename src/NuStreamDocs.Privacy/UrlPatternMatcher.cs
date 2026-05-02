// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy;

/// <summary>
/// Glob-pattern matcher for URL-level allow/exclude rules. Supports
/// <c>*</c> (any chars, including empty) and <c>?</c> (exactly one
/// char); every other byte is matched literally and case-
/// insensitively against ASCII letters. Patterns are anchored at both ends.
/// </summary>
internal sealed class UrlPatternMatcher
{
    /// <summary>Stored UTF-8 patterns; empty matcher matches nothing.</summary>
    private readonly byte[][] _patterns;

    /// <summary>Initializes a new instance of the <see cref="UrlPatternMatcher"/> class.</summary>
    /// <param name="patterns">UTF-8 glob patterns. Null or empty disables matching.</param>
    public UrlPatternMatcher(byte[][]? patterns)
    {
        if (patterns is null or [])
        {
            _patterns = [];
            return;
        }

        for (var i = 0; i < patterns.Length; i++)
        {
            if (patterns[i] is null or [])
            {
                throw new ArgumentException("Pattern entries must be non-empty.", nameof(patterns));
            }
        }

        _patterns = patterns;
    }

    /// <summary>Gets a value indicating whether any patterns are configured.</summary>
    public bool HasPatterns => _patterns is [_, ..];

    /// <summary>Returns true when at least one configured pattern matches <paramref name="url"/>.</summary>
    /// <param name="url">Candidate UTF-8 URL bytes.</param>
    /// <returns>True on match.</returns>
    public bool IsMatch(ReadOnlySpan<byte> url)
    {
        for (var i = 0; i < _patterns.Length; i++)
        {
            if (MatchGlob(_patterns[i], url))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Iterative glob match with single-star backtracking. Linear in the
    /// length of the input plus the pattern in the common case; the
    /// backtrack is bounded by the position of the most recent <c>*</c>.
    /// </summary>
    /// <param name="pattern">UTF-8 glob pattern.</param>
    /// <param name="input">UTF-8 candidate input.</param>
    /// <returns>True when the whole input matches the whole pattern.</returns>
    private static bool MatchGlob(ReadOnlySpan<byte> pattern, ReadOnlySpan<byte> input)
    {
        var p = 0;
        var i = 0;
        var starP = -1;
        var starI = 0;
        while (i < input.Length)
        {
            if (p < pattern.Length && (pattern[p] is (byte)'?' || EqualsIgnoreAscii(pattern[p], input[i])))
            {
                p++;
                i++;
                continue;
            }

            if (p < pattern.Length && pattern[p] is (byte)'*')
            {
                starP = p++;
                starI = i;
                continue;
            }

            if (starP < 0)
            {
                return false;
            }

            p = starP + 1;
            i = ++starI;
        }

        while (p < pattern.Length && pattern[p] is (byte)'*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    /// <summary>ASCII-byte case-insensitive equality (folds A-Z ↔ a-z only).</summary>
    /// <param name="a">First byte.</param>
    /// <param name="b">Second byte.</param>
    /// <returns>True when equal, or when both fold to the same ASCII letter.</returns>
    private static bool EqualsIgnoreAscii(byte a, byte b)
    {
        if (a == b)
        {
            return true;
        }

        const byte AsciiCaseBit = 0x20;
        var lo = (byte)(a | AsciiCaseBit);
        return lo == (b | AsciiCaseBit) && lo is >= (byte)'a' and <= (byte)'z';
    }
}
