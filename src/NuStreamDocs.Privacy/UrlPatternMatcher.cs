// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy;

/// <summary>
/// Glob-pattern matcher for URL-level allow/exclude rules. Supports
/// <c>*</c> (any chars, including empty) and <c>?</c> (exactly one
/// char); every other character is matched literally and case-
/// insensitively. Patterns are anchored at both ends.
/// </summary>
internal sealed class UrlPatternMatcher
{
    /// <summary>Stored patterns; empty matcher matches nothing.</summary>
    private readonly string[] _patterns;

    /// <summary>Initializes a new instance of the <see cref="UrlPatternMatcher"/> class.</summary>
    /// <param name="patterns">Glob patterns (case-insensitive). Null or empty disables matching.</param>
    public UrlPatternMatcher(string[]? patterns)
    {
        if (patterns is null or [])
        {
            _patterns = [];
            return;
        }

        for (var i = 0; i < patterns.Length; i++)
        {
            ArgumentException.ThrowIfNullOrEmpty(patterns[i]);
        }

        _patterns = patterns;
    }

    /// <summary>Gets a value indicating whether any patterns are configured.</summary>
    public bool HasPatterns => _patterns is [_, ..];

    /// <summary>Returns true when at least one configured pattern matches <paramref name="url"/>.</summary>
    /// <param name="url">Candidate URL.</param>
    /// <returns>True on match.</returns>
    public bool IsMatch(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var input = url.AsSpan();
        for (var i = 0; i < _patterns.Length; i++)
        {
            if (MatchGlob(_patterns[i].AsSpan(), input))
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
    /// <param name="pattern">Glob pattern.</param>
    /// <param name="input">Candidate input.</param>
    /// <returns>True when the whole input matches the whole pattern.</returns>
    private static bool MatchGlob(in ReadOnlySpan<char> pattern, in ReadOnlySpan<char> input)
    {
        var p = 0;
        var i = 0;
        var starP = -1;
        var starI = 0;
        while (i < input.Length)
        {
            if (p < pattern.Length && (pattern[p] is '?' || EqualsIgnoreCase(pattern[p], input[i])))
            {
                p++;
                i++;
                continue;
            }

            if (p < pattern.Length && pattern[p] is '*')
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

        while (p < pattern.Length && pattern[p] is '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    /// <summary>Case-insensitive char equality using invariant lowering.</summary>
    /// <param name="a">First char.</param>
    /// <param name="b">Second char.</param>
    /// <returns>True when equal under invariant lowering.</returns>
    private static bool EqualsIgnoreCase(char a, char b) =>
        a == b || char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
}
