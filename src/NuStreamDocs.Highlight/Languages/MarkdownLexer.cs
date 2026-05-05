// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Markdown lexer.</summary>
/// <remarks>
/// Pragmatic single-state Markdown lexer. Line-anchored rules classify
/// ATX heading prefixes (<c># ...</c>), fence markers (<c>```</c>),
/// list bullets, blockquote prefixes (<c>&gt;</c>), and horizontal rules.
/// Inline rules cover backtick code spans, bracketed link text, and bold /
/// italic emphasis runs. Plain text falls through unclassified so that
/// subsequent rendering can re-apply emphasis without overwriting.
/// </remarks>
public static class MarkdownLexer
{
    /// <summary>Length of the bare blockquote prefix (<c>&gt; </c>) plus its trailing space.</summary>
    private const int BlockquoteWithSpaceLength = 2;

    /// <summary>Length of a bullet marker plus its required trailing whitespace.</summary>
    private const int BulletWithSpaceLength = 2;

    /// <summary>Length of the bracket pair surrounding link text (<c>[</c> and <c>]</c>).</summary>
    private const int BracketPairLength = 2;

    /// <summary>First-byte set for whitespace runs.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for ATX heading prefix.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the fence marker rule.</summary>
    private static readonly SearchValues<byte> BacktickFirst = SearchValues.Create("`"u8);

    /// <summary>First-byte set for the tilde-fence rule.</summary>
    private static readonly SearchValues<byte> TildeFirst = SearchValues.Create("~"u8);

    /// <summary>First-byte set for blockquote prefix.</summary>
    private static readonly SearchValues<byte> AngleFirst = SearchValues.Create(">"u8);

    /// <summary>First-byte set for bullet markers (<c>-</c>, <c>*</c>, <c>+</c>).</summary>
    private static readonly SearchValues<byte> BulletFirst = SearchValues.Create("-*+"u8);

    /// <summary>First-byte set for emphasis runs (<c>*</c>, <c>_</c>).</summary>
    private static readonly SearchValues<byte> EmphasisFirst = SearchValues.Create("*_"u8);

    /// <summary>First-byte set for the bracket link rule.</summary>
    private static readonly SearchValues<byte> BracketFirst = SearchValues.Create("["u8);

    /// <summary>First-byte set for ordered-list markers.</summary>
    private static readonly SearchValues<byte> DigitFirst = TokenMatchers.AsciiDigits;

    /// <summary>Gets the singleton Markdown lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Markdown lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

            // ATX heading: # / ## / ... at line start, classify the whole line.
            new(MatchAtxHeading, TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = HashFirst, RequiresLineStart = true },

            // Fence opener / closer — line-anchored ``` or ~~~.
            new(MatchFenceLine, TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = BacktickFirst, RequiresLineStart = true },
            new(MatchFenceLine, TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = TildeFirst, RequiresLineStart = true },

            // Blockquote prefix (>).
            new(MatchBlockquotePrefix, TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = AngleFirst, RequiresLineStart = true },

            // Bullet at line start: optional indent, then -/+/* then space.
            new(MatchBulletPrefix, TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = BulletFirst, RequiresLineStart = true },

            // Ordered-list marker: digit run + . + space.
            new(MatchOrderedMarker, TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = DigitFirst, RequiresLineStart = true },

            // Inline code span: `...` (single backtick form only — multi-backtick deferred).
            new(MatchInlineCode, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = BacktickFirst },

            // Bracketed link text: [text].
            new(MatchBracketLink, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = BracketFirst },

            // Bold / italic emphasis run (one or more * or _).
            new(MatchEmphasisRun, TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = EmphasisFirst }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches an ATX heading line — one to six <c>#</c>s followed by space and content.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchAtxHeading(ReadOnlySpan<byte> slice)
    {
        const int MaxHeadingLevel = 6;
        var hashes = 0;
        while (hashes < slice.Length && hashes <= MaxHeadingLevel && slice[hashes] is (byte)'#')
        {
            hashes++;
        }

        if (hashes is 0 or > MaxHeadingLevel)
        {
            return 0;
        }

        if (hashes >= slice.Length || slice[hashes] is not ((byte)' ' or (byte)'\t'))
        {
            return 0;
        }

        return hashes + TokenMatchers.LineLength(slice[hashes..]);
    }

    /// <summary>Matches a fenced code-block opener / closer line — three or more backticks or tildes plus an optional info string.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchFenceLine(ReadOnlySpan<byte> slice)
    {
        const int MinFenceCount = 3;
        if (slice is [])
        {
            return 0;
        }

        var fenceByte = slice[0];
        if (fenceByte is not ((byte)'`' or (byte)'~'))
        {
            return 0;
        }

        var pos = 0;
        while (pos < slice.Length && slice[pos] == fenceByte)
        {
            pos++;
        }

        return pos < MinFenceCount ? 0 : pos + TokenMatchers.LineLength(slice[pos..]);
    }

    /// <summary>Matches a blockquote prefix — <c>&gt;</c> followed by an optional space.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBlockquotePrefix(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'>')
        {
            return 0;
        }

        return slice.Length > 1 && slice[1] is (byte)' ' ? BlockquoteWithSpaceLength : 1;
    }

    /// <summary>Matches a bullet marker — <c>-</c> / <c>*</c> / <c>+</c> followed by whitespace.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBulletPrefix(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < BulletWithSpaceLength)
        {
            return 0;
        }

        if (slice[0] is not ((byte)'-' or (byte)'*' or (byte)'+'))
        {
            return 0;
        }

        return slice[1] is (byte)' ' or (byte)'\t' ? BulletWithSpaceLength : 0;
    }

    /// <summary>Matches an ordered-list marker — digit run followed by <c>.</c> or <c>)</c> and whitespace.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchOrderedMarker(ReadOnlySpan<byte> slice)
    {
        var digits = TokenMatchers.MatchAsciiDigits(slice);
        if (digits is 0 || digits >= slice.Length)
        {
            return 0;
        }

        if (slice[digits] is not ((byte)'.' or (byte)')'))
        {
            return 0;
        }

        var afterDot = digits + 1;
        return afterDot < slice.Length && slice[afterDot] is (byte)' ' or (byte)'\t' ? afterDot + 1 : 0;
    }

    /// <summary>Matches a single-backtick inline code span — <c>`...`</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchInlineCode(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchBracketedBlock(slice, (byte)'`', (byte)'`');

    /// <summary>Matches a bracketed link-text span — <c>[text]</c>. Closing <c>]</c> must appear on the same line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBracketLink(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'[')
        {
            return 0;
        }

        var lineLen = TokenMatchers.LineLength(slice);
        var span = slice[..lineLen];
        var close = span[1..].IndexOf((byte)']');
        return close < 0 ? 0 : BracketPairLength + close;
    }

    /// <summary>Matches a run of emphasis markers (one or more <c>*</c> or <c>_</c>) up to a length cap.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchEmphasisRun(ReadOnlySpan<byte> slice)
    {
        const int MaxRunLength = 3;
        if (slice is [])
        {
            return 0;
        }

        var marker = slice[0];
        if (marker is not ((byte)'*' or (byte)'_'))
        {
            return 0;
        }

        var pos = 0;
        while (pos < slice.Length && pos < MaxRunLength && slice[pos] == marker)
        {
            pos++;
        }

        return pos;
    }
}
