// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Unified-diff lexer.</summary>
/// <remarks>
/// Pygments classifies diff lines into <c>Generic.Inserted</c> (<c>gi</c>),
/// <c>Generic.Deleted</c> (<c>gd</c>), <c>Generic.Subheading</c> (<c>gu</c>)
/// and <c>Generic.Heading</c> (<c>gh</c>). To stay within the existing
/// taxonomy without adding new enum values, we map:
/// <list type="bullet">
/// <item>added (<c>+...</c>) → <see cref="TokenClass.StringEscape"/> (<c>se</c>) — themes pick a green tone</item>
/// <item>removed (<c>-...</c>) → <see cref="TokenClass.CommentPreproc"/> (<c>cp</c>)</item>
/// <item>hunk header (<c>@@</c>) → <see cref="TokenClass.CommentSpecial"/> (<c>cs</c>)</item>
/// <item>file header (<c>---</c>/<c>+++</c>) → <see cref="TokenClass.CommentMulti"/> (<c>cm</c>)</item>
/// </list>
/// </remarks>
public static class DiffLexer
{
    /// <summary>First-char set for file-header lines (<c>---</c>, <c>+++</c>, <c>diff</c>, <c>index</c>, <c>Only in</c>).</summary>
    private static readonly SearchValues<char> FileHeaderFirst = SearchValues.Create("-+diO");

    /// <summary>First-char set for hunk markers (<c>@@</c>).</summary>
    private static readonly SearchValues<char> HunkFirst = SearchValues.Create("@");

    /// <summary>First-char set for added lines (<c>+...</c>).</summary>
    private static readonly SearchValues<char> AddedFirst = SearchValues.Create("+");

    /// <summary>First-char set for removed lines (<c>-...</c>).</summary>
    private static readonly SearchValues<char> RemovedFirst = SearchValues.Create("-");

    /// <summary>First-char set for newline tokens.</summary>
    private static readonly SearchValues<char> NewlineFirst = SearchValues.Create("\r\n");

    /// <summary>File-header line prefixes — checked in declaration order; longest first to match the regex alternation contract.</summary>
    private static readonly string[] FileHeaderPrefixes = ["---", "+++", "diff ", "index ", "Only in "];

    /// <summary>Gets the singleton lexer instance.</summary>
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1115", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "This is a lexer definition, not a documentation comment.")]
    public static Lexer Instance { get; } = new(
        "diff",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = [

                // File-header line: ---, +++, "diff ", "index ", "Only in " followed by the rest of the line.
                new(MatchFileHeader, TokenClass.CommentMulti, NextState: null) { FirstChars = FileHeaderFirst, RequiresLineStart = true },

                // Hunk header: @@ ... line.
                new(static slice => slice is ['@', '@', ..] ? TokenMatchers.LineLength(slice) : 0, TokenClass.CommentSpecial, NextState: null) { FirstChars = HunkFirst, RequiresLineStart = true },

                // Added line: + ... line.
                new(static slice => slice is ['+', ..] ? TokenMatchers.LineLength(slice) : 0, TokenClass.StringEscape, NextState: null) { FirstChars = AddedFirst, RequiresLineStart = true },

                // Removed line: - ... line.
                new(static slice => slice is ['-', ..] ? TokenMatchers.LineLength(slice) : 0, TokenClass.CommentPreproc, NextState: null) { FirstChars = RemovedFirst, RequiresLineStart = true },

                // Context line: anything else, line-anchored.
                new(MatchContext, TokenClass.Text, NextState: null) { RequiresLineStart = true },

                // Line terminator (\r\n, \r, \n).
                new(TokenMatchers.MatchNewline, TokenClass.Whitespace, NextState: null) { FirstChars = NewlineFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));

    /// <summary>One file-header line: a known prefix followed by the rest of the line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the line on match (excluding the terminator).</returns>
    private static int MatchFileHeader(ReadOnlySpan<char> slice)
    {
        var prefix = TokenMatchers.MatchLongestLiteral(slice, FileHeaderPrefixes);
        return prefix is 0 ? 0 : prefix + TokenMatchers.LineLength(slice[prefix..]);
    }

    /// <summary>One context line — anything that isn't a file/hunk/added/removed marker or an empty line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the line on match.</returns>
    private static int MatchContext(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is '+' or '-' or '@' or '\r' or '\n')
        {
            return 0;
        }

        return TokenMatchers.LineLength(slice);
    }
}
