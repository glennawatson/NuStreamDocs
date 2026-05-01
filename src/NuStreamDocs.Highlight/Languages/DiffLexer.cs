// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Unified-diff lexer.</summary>
/// <remarks>
/// Pygments classifies diff lines into <c>Generic.Inserted</c> (<c>gi</c>),
/// <c>Generic.Deleted</c> (<c>gd</c>), <c>Generic.Subheading</c> (<c>gu</c>)
/// and <c>Generic.Heading</c> (<c>gh</c>). To stay within the existing
/// taxonomy without adding new enum values, we map:
/// <list type="bullet">
/// <item>added (<c>+...</c>) → <see cref="TokenClass.StringEscape"/> (<c>se</c>) — themes pick a green tone</item>
/// <item>removed (<c>-...</c>) → <see cref="TokenClass.NameClass"/> isn't right; use <see cref="TokenClass.CommentPreproc"/> (<c>cp</c>) which themes typically tint distinctively</item>
/// <item>hunk header (<c>@@</c>) → <see cref="TokenClass.CommentSpecial"/> (<c>cs</c>)</item>
/// <item>file header (<c>---</c>/<c>+++</c>) → <see cref="TokenClass.CommentMulti"/> (<c>cm</c>)</item>
/// </list>
/// Themes that rely on Pygments' generic classes for diffs may want a
/// thin CSS shim mapping <c>gi/gd/gu/gh</c> → these classes; the
/// content still highlights without it.
/// </remarks>
public static partial class DiffLexer
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

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "diff",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] =
            [
                new(FileHeaderRegex(), TokenClass.CommentMulti, NextState: null) { FirstChars = FileHeaderFirst },
                new(HunkRegex(), TokenClass.CommentSpecial, NextState: null) { FirstChars = HunkFirst },
                new(AddedRegex(), TokenClass.StringEscape, NextState: null) { FirstChars = AddedFirst },
                new(RemovedRegex(), TokenClass.CommentPreproc, NextState: null) { FirstChars = RemovedFirst },
                new(ContextRegex(), TokenClass.Text, NextState: null),
                new(NewlineRegex(), TokenClass.Whitespace, NextState: null) { FirstChars = NewlineFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));

    [GeneratedRegex(@"\G^(?:---|\+\+\+|diff |index |Only in ).*$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex FileHeaderRegex();

    [GeneratedRegex(@"\G^@@.*$", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex HunkRegex();

    [GeneratedRegex(@"\G^\+[^\r\n]*", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex AddedRegex();

    [GeneratedRegex(@"\G^-[^\r\n]*", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex RemovedRegex();

    [GeneratedRegex(@"\G^[^\+\-@\r\n][^\r\n]*", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex ContextRegex();

    [GeneratedRegex(@"\G\r?\n", RegexOptions.Compiled)]
    private static partial Regex NewlineRegex();
}
