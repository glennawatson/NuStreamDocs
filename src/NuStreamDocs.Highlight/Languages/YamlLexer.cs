// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>YAML lexer.</summary>
/// <remarks>
/// Pragmatic subset of Pygments' <c>YamlLexer</c>: comments, mapping
/// keys (followed by <c>:</c>), document separators, anchors / aliases,
/// quoted strings, numbers, and literal constants. Block scalars
/// (<c>|</c>, <c>&gt;</c>) are recognised at the indicator and their
/// payloads fall through as plain text — fine for typical config docs.
/// </remarks>
public static class YamlLexer
{
    /// <summary>Minimum length of a list-bullet token (<c>-</c> + a whitespace separator).</summary>
    private const int BulletMinimumLength = 2;

    /// <summary>First-char set for the <c>#</c> line-comment indicator.</summary>
    private static readonly SearchValues<char> CommentFirst = SearchValues.Create("#");

    /// <summary>First-char set for YAML anchors (<c>&amp;name</c>).</summary>
    private static readonly SearchValues<char> AnchorFirst = SearchValues.Create("&");

    /// <summary>First-char set for YAML aliases (<c>*name</c>).</summary>
    private static readonly SearchValues<char> AliasFirst = SearchValues.Create("*");

    /// <summary>First-char set for YAML tag indicators (<c>!</c> / <c>!!</c>).</summary>
    private static readonly SearchValues<char> TagFirst = SearchValues.Create("!");

    /// <summary>First-char set for plain identifiers and plain keys (letters, underscore).</summary>
    private static readonly SearchValues<char> IdentifierFirst = TokenMatchers.AsciiIdentifierStart;

    /// <summary>First-char set for block scalar indicators (<c>|</c>, <c>&gt;</c>).</summary>
    private static readonly SearchValues<char> BlockScalarFirst = SearchValues.Create("|>");

    /// <summary>First-char set for the case-insensitive YAML keyword constants.</summary>
    private static readonly SearchValues<char> KeywordConstantFirst = SearchValues.Create("tTfFnNyYoO~");

    /// <summary>First-char set for numeric tokens.</summary>
    private static readonly SearchValues<char> NumberFirst = SearchValues.Create("-0123456789");

    /// <summary>First-char set for flow-style structural punctuation.</summary>
    private static readonly SearchValues<char> PunctuationFirst = SearchValues.Create("{}[],:");

    /// <summary>Allowed characters in plain-key continuation: letters, digits, underscore, dot, dash.</summary>
    private static readonly SearchValues<char> PlainKeyContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.-");

    /// <summary>Allowed characters in anchor / alias names: letters, digits, underscore, dash.</summary>
    private static readonly SearchValues<char> AnchorBody = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-");

    /// <summary>Allowed characters in YAML tag bodies: letters, digits, underscore, slash, dash.</summary>
    private static readonly SearchValues<char> TagBody = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_/-");

    /// <summary>Set of YAML literal constants — case-insensitive comparer matches Pygments' <c>IgnoreCase</c>.</summary>
    private static readonly FrozenSet<string> KeywordConstants = FrozenSet.ToFrozenSet(
        ["true", "false", "null", "yes", "no", "on", "off"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Document-separator alternatives — longest first.</summary>
    private static readonly string[] DocumentSeparators = ["---", "..."];

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "yaml",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = [

                // [ \t\r\n]+ whitespace runs.
                new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, NextState: null) { FirstChars = TokenMatchers.AsciiWhitespaceWithNewlines },

                // # line comment to end-of-line.
                new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, NextState: null) { FirstChars = CommentFirst },

                // --- / ... document separator (no first-char hint — line-anchored use).
                new(static slice => TokenMatchers.MatchLongestLiteral(slice, DocumentSeparators), TokenClass.CommentPreproc, NextState: null),

                // &name anchor declaration.
                new(static slice => TokenMatchers.MatchPrefixedRun(slice, '&', AnchorBody), TokenClass.NameClass, NextState: null) { FirstChars = AnchorFirst },

                // *name alias reference.
                new(static slice => TokenMatchers.MatchPrefixedRun(slice, '*', AnchorBody), TokenClass.NameClass, NextState: null) { FirstChars = AliasFirst },

                // ! / !! tag indicator.
                new(MatchTag, TokenClass.NameAttribute, NextState: null) { FirstChars = TagFirst },

                // "..." quoted mapping key — must precede the plain string-double rule.
                new(TokenMatchers.MatchDoubleQuotedKey, TokenClass.NameAttribute, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst },

                // Plain mapping key — identifier shape followed by ':' lookahead.
                new(MatchKeyPlain, TokenClass.NameAttribute, NextState: null) { FirstChars = IdentifierFirst },

                // "..." double-quoted string with backslash escapes.
                new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst },

                // '...' single-quoted string with '' as the embedded-quote escape (YAML/SQL style).
                new(TokenMatchers.MatchSingleQuotedDoubledEscape, TokenClass.StringSingle, NextState: null) { FirstChars = LanguageCommon.SingleQuoteFirst },

                // | / > block-scalar indicator with optional + - chomping and digit indent.
                new(MatchBlockScalarIndicator, TokenClass.Punctuation, NextState: null) { FirstChars = BlockScalarFirst },

                // Case-insensitive YAML literal constants: true / false / null / yes / no / on / off / ~.
                new(MatchKeywordConstant, TokenClass.KeywordConstant, NextState: null) { FirstChars = KeywordConstantFirst },

                // -?\d+\.\d+([eE][+-]?\d+)? float literal — must precede the integer rule.
                new(TokenMatchers.MatchSignedAsciiFloat, TokenClass.NumberFloat, NextState: null) { FirstChars = NumberFirst },

                // -?\d+ integer literal.
                new(TokenMatchers.MatchSignedAsciiInteger, TokenClass.NumberInteger, NextState: null) { FirstChars = NumberFirst },

                // List bullet — line-anchored, optional indentation + '-' + whitespace.
                new(MatchBullet, TokenClass.Punctuation, NextState: null) { RequiresLineStart = true },

                // Flow-style structural punctuation: { } [ ] , :
                new(static slice => TokenMatchers.MatchSingleCharOf(slice, PunctuationFirst), TokenClass.Punctuation, NextState: null) { FirstChars = PunctuationFirst },

                // Plain identifier — letters/digits/underscore/dot/dash, must start with letter or underscore.
                new(static slice => TokenMatchers.MatchIdentifier(slice, IdentifierFirst, PlainKeyContinue), TokenClass.Name, NextState: null) { FirstChars = IdentifierFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));

    /// <summary>YAML tag — <c>!</c> or <c>!!</c> optionally followed by name characters.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchTag(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not '!')
        {
            return 0;
        }

        var pos = 1;
        if (pos < slice.Length && slice[pos] is '!')
        {
            pos++;
        }

        var bodyEnd = slice[pos..].IndexOfAnyExcept(TagBody);
        return pos + (bodyEnd < 0 ? slice.Length - pos : bodyEnd);
    }

    /// <summary>Plain mapping key — identifier-shape followed by optional whitespace and <c>:</c> followed by whitespace or end-of-input.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchKeyPlain(ReadOnlySpan<char> slice)
    {
        var nameLen = TokenMatchers.MatchIdentifier(slice, IdentifierFirst, PlainKeyContinue);
        if (nameLen is 0)
        {
            return 0;
        }

        var afterName = slice[nameLen..];
        var ws = TokenMatchers.MatchAsciiWhitespace(afterName);
        var colonAt = nameLen + ws;
        if (colonAt >= slice.Length || slice[colonAt] is not ':')
        {
            return 0;
        }

        // Pygments requires the colon to be followed by whitespace or end-of-input.
        var afterColon = colonAt + 1;
        if (afterColon >= slice.Length || TokenMatchers.AsciiWhitespaceWithNewlines.Contains(slice[afterColon]))
        {
            return nameLen;
        }

        return 0;
    }

    /// <summary>Block-scalar indicator — <c>|</c> or <c>&gt;</c>, optional <c>+</c>/<c>-</c>, optional digit.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchBlockScalarIndicator(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not ('|' or '>'))
        {
            return 0;
        }

        var pos = 1;
        if (pos < slice.Length && slice[pos] is '+' or '-')
        {
            pos++;
        }

        if (pos < slice.Length && TokenMatchers.AsciiDigits.Contains(slice[pos]))
        {
            pos++;
        }

        return pos;
    }

    /// <summary>YAML literal constant — case-insensitive keyword from <see cref="KeywordConstants"/>, or the bare <c>~</c> null sigil.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchKeywordConstant(ReadOnlySpan<char> slice)
    {
        // Pygments' set includes "~" as a single-character null literal.
        if (slice is ['~', ..])
        {
            return 1;
        }

        return TokenMatchers.MatchKeywordIgnoreCase(slice, KeywordConstants);
    }

    /// <summary>List bullet at line start — optional indentation, then <c>-</c>, then a whitespace separator.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchBullet(ReadOnlySpan<char> slice)
    {
        var indent = TokenMatchers.MatchAsciiInlineWhitespace(slice);
        if (indent + BulletMinimumLength > slice.Length || slice[indent] is not '-')
        {
            return 0;
        }

        if (!TokenMatchers.AsciiWhitespaceWithNewlines.Contains(slice[indent + 1]))
        {
            return 0;
        }

        return indent + BulletMinimumLength;
    }
}
