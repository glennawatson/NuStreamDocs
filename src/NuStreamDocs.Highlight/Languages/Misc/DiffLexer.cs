// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Misc;

/// <summary>Unified-diff lexer.</summary>
/// <remarks>
/// Each diff-line shape gets its own descriptive token class; the CSS
/// short forms match the established taxonomy so existing themes
/// (mkdocs-material, GitHub's diff styling) light up unchanged:
/// <list type="bullet">
/// <item>added (<c>+...</c>) → <see cref="TokenClass.DiffAddedLine"/> (<c>gi</c>)</item>
/// <item>removed (<c>-...</c>) → <see cref="TokenClass.DiffRemovedLine"/> (<c>gd</c>)</item>
/// <item>hunk header (<c>@@ ... @@</c>) → <see cref="TokenClass.DiffHunkHeader"/> (<c>gu</c>)</item>
/// <item>file header (<c>---</c> / <c>+++</c> / <c>diff …</c> / <c>index …</c> / <c>Only in …</c>) → <see cref="TokenClass.DiffFileHeader"/> (<c>gh</c>)</item>
/// </list>
/// </remarks>
public static class DiffLexer
{
    /// <summary>First-byte set for file-header lines (<c>---</c>, <c>+++</c>, <c>diff</c>, <c>index</c>, <c>Only in</c>).</summary>
    private static readonly SearchValues<byte> FileHeaderFirst = SearchValues.Create("-+diO"u8);

    /// <summary>First-byte set for hunk markers (<c>@@</c>).</summary>
    private static readonly SearchValues<byte> HunkFirst = SearchValues.Create("@"u8);

    /// <summary>First-byte set for added lines (<c>+...</c>).</summary>
    private static readonly SearchValues<byte> AddedFirst = SearchValues.Create("+"u8);

    /// <summary>First-byte set for removed lines (<c>-...</c>).</summary>
    private static readonly SearchValues<byte> RemovedFirst = SearchValues.Create("-"u8);

    /// <summary>First-byte set for newline tokens.</summary>
    private static readonly SearchValues<byte> NewlineFirst = SearchValues.Create("\r\n"u8);

    /// <summary>Bytes that disqualify a context line — markers handled by their own rule plus the line terminators.</summary>
    private static readonly SearchValues<byte> ContextStopFirst = SearchValues.Create("+-@\r\n"u8);

    /// <summary>File-header line prefixes — checked in declaration order; longest first so a multi-byte prefix wins before its shorter substring.</summary>
    private static readonly byte[][] FileHeaderPrefixes =
    [
        [.. "---"u8],
        [.. "+++"u8],
        [.. "diff "u8],
        [.. "index "u8],
        [.. "Only in "u8]
    ];

    /// <summary>Gets the singleton lexer instance.</summary>
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1115", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("Major Code Smell", "S125:Sections of code should not be commented out", Justification = "Not commented out code")]
    public static Lexer Instance { get; } = new(
        LanguageRuleBuilder.BuildSingleState([

            // File-header line: ---, +++, "diff ", "index ", "Only in " followed by the rest of the line.
            new(
                static slice => TokenMatchers.MatchPrefixedLineLongest(
                    slice,
                    FileHeaderPrefixes),
                TokenClass.DiffFileHeader,
                LexerRule.NoStateChange)
            {
                FirstBytes = FileHeaderFirst,
                RequiresLineStart = true
            },

            // Hunk header: @@ ... line.
            new(
                static slice => TokenMatchers.MatchPrefixedLine(slice, (byte)'@', (byte)'@'),
                TokenClass.DiffHunkHeader,
                LexerRule.NoStateChange)
            {
                FirstBytes = HunkFirst,
                RequiresLineStart = true
            },

            // Added line: + ... line.
            new(
                static slice => TokenMatchers.MatchPrefixedLine(slice, (byte)'+'),
                TokenClass.DiffAddedLine,
                LexerRule.NoStateChange)
            {
                FirstBytes = AddedFirst,
                RequiresLineStart = true
            },

            // Removed line: - ... line.
            new(
                static slice => TokenMatchers.MatchPrefixedLine(slice, (byte)'-'),
                TokenClass.DiffRemovedLine,
                LexerRule.NoStateChange)
            {
                FirstBytes = RemovedFirst,
                RequiresLineStart = true
            },

            // Context line: anything not starting with +, -, @ or a line terminator.
            new(
                static slice => TokenMatchers.MatchLineUnlessStartsWith(slice, ContextStopFirst),
                TokenClass.Text,
                LexerRule.NoStateChange)
            {
                RequiresLineStart = true
            },

            // Line terminator (\r\n, \r, \n).
            new(
                TokenMatchers.MatchNewline,
                TokenClass.Whitespace,
                LexerRule.NoStateChange)
            {
                FirstBytes = NewlineFirst
            }
        ]));
}
