// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Reusable YAML rule list factory. Extracted as a helper so future
/// YAML-embedding lexers (Ansible playbooks, Helm chart templates,
/// GitHub Actions workflow files) classify YAML tokens identically.
/// </summary>
internal static class YamlRules
{
    /// <summary>Minimum length of a list-bullet token (<c>-</c> + a whitespace separator).</summary>
    private const int BulletMinimumLength = 2;

    /// <summary>First-byte set for the <c>#</c> line-comment indicator.</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for YAML anchors (<c>&amp;name</c>).</summary>
    private static readonly SearchValues<byte> AnchorFirst = SearchValues.Create("&"u8);

    /// <summary>First-byte set for YAML aliases (<c>*name</c>).</summary>
    private static readonly SearchValues<byte> AliasFirst = SearchValues.Create("*"u8);

    /// <summary>First-byte set for YAML tag indicators (<c>!</c> / <c>!!</c>).</summary>
    private static readonly SearchValues<byte> TagFirst = SearchValues.Create("!"u8);

    /// <summary>First-byte set for plain identifiers and plain keys (letters, underscore).</summary>
    private static readonly SearchValues<byte> IdentifierFirst = TokenMatchers.AsciiIdentifierStart;

    /// <summary>First-byte set for block scalar indicators (<c>|</c>, <c>&gt;</c>).</summary>
    private static readonly SearchValues<byte> BlockScalarFirst = SearchValues.Create("|>"u8);

    /// <summary>First-byte set for the case-insensitive YAML keyword constants.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tTfFnNyYoO~"u8);

    /// <summary>First-byte set for numeric tokens.</summary>
    private static readonly SearchValues<byte> NumberFirst = SearchValues.Create("-0123456789"u8);

    /// <summary>First-byte set for flow-style structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationFirst = SearchValues.Create("{}[],:"u8);

    /// <summary>Allowed bytes in plain-key continuation: letters, digits, underscore, dot, dash.</summary>
    private static readonly SearchValues<byte> PlainKeyContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.-"u8);

    /// <summary>Allowed bytes in anchor / alias names: letters, digits, underscore, dash.</summary>
    private static readonly SearchValues<byte> AnchorBody = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-"u8);

    /// <summary>Allowed bytes in YAML tag bodies: letters, digits, underscore, slash, dash.</summary>
    private static readonly SearchValues<byte> TagBody = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_/-"u8);

    /// <summary>Set of YAML literal constants — case-insensitive lookup.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateIgnoreCase(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8],
        [.. "yes"u8],
        [.. "no"u8],
        [.. "on"u8],
        [.. "off"u8]);

    /// <summary>Document-separator alternatives — longest first.</summary>
    private static readonly byte[][] DocumentSeparators =
    [
        [.. "---"u8],
        [.. "..."u8]
    ];

    /// <summary>
    /// Builds the YAML rule list. Order matters — quoted-key precedes
    /// plain-key precedes plain-string; flow punctuation precedes plain
    /// identifier so a leading <c>:</c> doesn't get eaten.
    /// </summary>
    /// <returns>Ordered rule list classifying YAML tokens.</returns>
    public static LexerRule[] Build() =>
    [

        // [ \t\r\n]+ whitespace runs.
        new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiWhitespaceWithNewlines },

        // # line comment to end-of-line.
        new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = CommentFirst },

        // --- / ... document separator (no first-byte hint — line-anchored use).
        new(static slice => TokenMatchers.MatchLongestLiteral(slice, DocumentSeparators), TokenClass.CommentPreproc, LexerRule.NoStateChange),

        // &name anchor declaration.
        new(static slice => TokenMatchers.MatchPrefixedRun(slice, (byte)'&', AnchorBody), TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = AnchorFirst },

        // *name alias reference.
        new(static slice => TokenMatchers.MatchPrefixedRun(slice, (byte)'*', AnchorBody), TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = AliasFirst },

        // ! / !! tag indicator.
        new(MatchTag, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = TagFirst },

        // "..." quoted mapping key — must precede the plain string-double rule.
        new(TokenMatchers.MatchDoubleQuotedKey, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

        // Plain mapping key — identifier shape followed by ':' lookahead.
        new(MatchKeyPlain, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = IdentifierFirst },

        // "..." double-quoted string with backslash escapes.
        new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

        // '...' single-quoted string with '' as the embedded-quote escape (YAML/SQL style).
        new(TokenMatchers.MatchSingleQuotedDoubledEscape, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

        // | / > block-scalar indicator with optional + - chomping and digit indent.
        new(MatchBlockScalarIndicator, TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = BlockScalarFirst },

        // Case-insensitive YAML literal constants: true / false / null / yes / no / on / off / ~.
        new(MatchKeywordConstant, TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },

        // -?\d+\.\d+([eE][+-]?\d+)? float literal — must precede the integer rule.
        new(TokenMatchers.MatchSignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = NumberFirst },

        // -?\d+ integer literal.
        new(TokenMatchers.MatchSignedAsciiInteger, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = NumberFirst },

        // List bullet — line-anchored, optional indentation + '-' + whitespace.
        new(MatchBullet, TokenClass.Punctuation, LexerRule.NoStateChange) { RequiresLineStart = true },

        // Flow-style structural punctuation: { } [ ] , :
        new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationFirst), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationFirst },

        // Plain identifier — letters/digits/underscore/dot/dash, must start with letter or underscore.
        new(static slice => TokenMatchers.MatchIdentifier(slice, IdentifierFirst, PlainKeyContinue), TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = IdentifierFirst }
    ];

    /// <summary>YAML tag — <c>!</c> or <c>!!</c> optionally followed by name bytes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchTag(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'!')
        {
            return 0;
        }

        var pos = 1;
        if (pos < slice.Length && slice[pos] is (byte)'!')
        {
            pos++;
        }

        var bodyEnd = slice[pos..].IndexOfAnyExcept(TagBody);
        return pos + (bodyEnd < 0 ? slice.Length - pos : bodyEnd);
    }

    /// <summary>Plain mapping key — identifier-shape followed by optional whitespace and <c>:</c> followed by whitespace or end-of-input.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchKeyPlain(ReadOnlySpan<byte> slice)
    {
        var nameLen = TokenMatchers.MatchIdentifier(slice, IdentifierFirst, PlainKeyContinue);
        if (nameLen is 0)
        {
            return 0;
        }

        var afterName = slice[nameLen..];
        var ws = TokenMatchers.MatchAsciiWhitespace(afterName);
        var colonAt = nameLen + ws;
        if (colonAt >= slice.Length || slice[colonAt] is not (byte)':')
        {
            return 0;
        }

        // The colon must be followed by whitespace or end-of-input — otherwise it's part of a value.
        var afterColon = colonAt + 1;
        return afterColon >= slice.Length || TokenMatchers.AsciiWhitespaceWithNewlines.Contains(slice[afterColon]) ? nameLen : 0;
    }

    /// <summary>Block-scalar indicator — <c>|</c> or <c>&gt;</c>, optional <c>+</c>/<c>-</c>, optional digit.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchBlockScalarIndicator(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not ((byte)'|' or (byte)'>'))
        {
            return 0;
        }

        var pos = 1;
        if (pos < slice.Length && slice[pos] is (byte)'+' or (byte)'-')
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
    private static int MatchKeywordConstant(ReadOnlySpan<byte> slice) =>

        // YAML's spec includes "~" as a single-byte null literal.
        slice is [(byte)'~', ..] ? 1 : TokenMatchers.MatchKeyword(slice, KeywordConstants);

    /// <summary>List bullet at line start — optional indentation, then <c>-</c>, then a whitespace separator.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchBullet(ReadOnlySpan<byte> slice)
    {
        var indent = TokenMatchers.MatchAsciiInlineWhitespace(slice);
        if (indent + BulletMinimumLength > slice.Length || slice[indent] is not (byte)'-')
        {
            return 0;
        }

        return !TokenMatchers.AsciiWhitespaceWithNewlines.Contains(slice[indent + 1]) ? 0 : indent + BulletMinimumLength;
    }
}
