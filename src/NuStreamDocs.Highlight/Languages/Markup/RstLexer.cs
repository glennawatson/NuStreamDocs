// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Markup;

/// <summary>reStructuredText (RST) lexer.</summary>
/// <remarks>
/// Single-state RST scanner covering the inline + block markers a reader sees
/// most often: directives (<c>.. directive:: …</c>), comments (<c>..</c> at line
/// start), inline literals (<c>``text``</c>), strong / emphasis runs
/// (<c>**bold**</c>, <c>*italic*</c>), interpreted-text and roles
/// (<c>:role:</c>, <c>`text`</c>), substitutions (<c>|name|</c>), heading
/// underlines (lines made entirely of <c>= - ~ ^ " ' + * :</c> repeated), bullet
/// / enumerated list markers, and field-list <c>:field:</c> labels.
/// </remarks>
public static class RstLexer
{
    /// <summary>Length of the <c>..</c> directive / comment introducer.</summary>
    private const int DirectiveIntroducerLength = 2;

    /// <summary>First-byte set for whitespace.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the <c>..</c> directive / comment dispatch.</summary>
    private static readonly SearchValues<byte> DotFirst = SearchValues.Create("."u8);

    /// <summary>First-byte set for the inline-literal <c>``…``</c> rule.</summary>
    private static readonly SearchValues<byte> BacktickFirst = SearchValues.Create("`"u8);

    /// <summary>First-byte set for emphasis / strong-emphasis runs.</summary>
    private static readonly SearchValues<byte> AsteriskFirst = SearchValues.Create("*"u8);

    /// <summary>First-byte set for substitution / pipe markers.</summary>
    private static readonly SearchValues<byte> PipeFirst = SearchValues.Create("|"u8);

    /// <summary>First-byte set for the heading-underline rule (every byte that an underline character can be).</summary>
    private static readonly SearchValues<byte> UnderlineFirst = SearchValues.Create("=-~^\"'+*:#"u8);

    /// <summary>First-byte set for the bullet-marker rule (<c>- * + </c> + space).</summary>
    private static readonly SearchValues<byte> BulletFirst = SearchValues.Create("-*+"u8);

    /// <summary>First-byte set for the role / field-list rule (<c>:</c>).</summary>
    private static readonly SearchValues<byte> ColonFirst = SearchValues.Create(":"u8);

    /// <summary>Identifier-continuation set for directive names (letters / digits / dash / underscore).</summary>
    private static readonly SearchValues<byte> NameContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-"u8);

    /// <summary>Bytes that may appear as a heading-underline character (a line consisting entirely of these classifies as a heading marker).</summary>
    private static readonly SearchValues<byte> UnderlineChars = SearchValues.Create("=-~^\"'+*:#"u8);

    /// <summary>Gets the singleton RST lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the RST lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

            // ".. directive:: …" or ".." comment — line-anchored.
            new(MatchDirectiveOrComment, TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = DotFirst, RequiresLineStart = true },

            // Heading underline — line-anchored.
            new(MatchHeadingUnderline, TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = UnderlineFirst, RequiresLineStart = true },

            // List bullet — line-anchored.
            new(MatchBulletMarker, TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = BulletFirst, RequiresLineStart = true },

            // Field list — :field: at line start.
            new(MatchFieldName, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = ColonFirst, RequiresLineStart = true },

            // ``inline literal`` — must precede the single-backtick interpreted-text rule.
            new(MatchInlineLiteral, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = BacktickFirst },

            // `interpreted text` (with optional :role: prefix or _ trailer).
            new(MatchInterpretedText, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = BacktickFirst },

            // **strong** — must precede *emphasis*.
            new(MatchStrongOrEmphasis, TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = AsteriskFirst },

            // |substitution| reference.
            new(MatchSubstitution, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = PipeFirst },

            // :role: standalone (e.g. interpreted text role prefix without text).
            new(MatchRoleColon, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = ColonFirst }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches a directive (<c>.. name::</c>) or a comment line (<c>..</c> followed by free text).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the line, or zero on miss.</returns>
    private static int MatchDirectiveOrComment(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < DirectiveIntroducerLength
            || slice[0] is not (byte)'.'
            || slice[1] is not (byte)'.')
        {
            return 0;
        }

        // Bare ".." with optional whitespace + content; the whole line is consumed.
        return TokenMatchers.LineLength(slice);
    }

    /// <summary>Matches a heading-underline line — three or more identical valid underline characters with no other content.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the underline line, or zero.</returns>
    private static int MatchHeadingUnderline(ReadOnlySpan<byte> slice)
    {
        const int MinimumUnderlineRun = 3;
        if (slice is [] || !UnderlineChars.Contains(slice[0]))
        {
            return 0;
        }

        var runLen = ConsumeRun(slice, slice[0]);
        if (runLen < MinimumUnderlineRun)
        {
            return 0;
        }

        var afterRun = runLen + ConsumeSpaces(slice[runLen..]);
        return IsLineTerminated(slice, afterRun) ? afterRun : 0;
    }

    /// <summary>Consumes a run of <paramref name="marker"/> bytes from the start of <paramref name="slice"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="marker">Marker byte.</param>
    /// <returns>Length of the run.</returns>
    private static int ConsumeRun(ReadOnlySpan<byte> slice, byte marker)
    {
        var pos = 0;
        while (pos < slice.Length && slice[pos] == marker)
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Consumes a run of ASCII spaces from the start of <paramref name="slice"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the run.</returns>
    private static int ConsumeSpaces(ReadOnlySpan<byte> slice)
    {
        var pos = 0;
        while (pos < slice.Length && slice[pos] is (byte)' ')
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Returns true when <paramref name="pos"/> in <paramref name="slice"/> is at a newline byte or beyond the end of <paramref name="slice"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="pos">Index to test.</param>
    /// <returns>True at end-of-line / end-of-input.</returns>
    private static bool IsLineTerminated(ReadOnlySpan<byte> slice, int pos) =>
        pos >= slice.Length || slice[pos] is (byte)'\n' or (byte)'\r';

    /// <summary>Matches a list-bullet marker — <c>-</c> / <c>*</c> / <c>+</c> followed by a whitespace byte.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (2 bytes — marker + space), or zero.</returns>
    private static int MatchBulletMarker(ReadOnlySpan<byte> slice)
    {
        const int BulletMarkerLength = 2;
        if (slice.Length < BulletMarkerLength)
        {
            return 0;
        }

        if (slice[0] is not ((byte)'-' or (byte)'*' or (byte)'+'))
        {
            return 0;
        }

        return slice[1] is (byte)' ' or (byte)'\t' ? BulletMarkerLength : 0;
    }

    /// <summary>Matches a field-list label — <c>:fieldname:</c> at line start.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchFieldName(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 3 || slice[0] is not (byte)':' || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var stop = slice[2..].IndexOfAnyExcept(NameContinue);
        var nameLen = stop < 0 ? slice.Length - 2 : stop;
        var afterName = 1 + 1 + nameLen;
        return afterName < slice.Length && slice[afterName] is (byte)':' ? afterName + 1 : 0;
    }

    /// <summary>Matches an inline-literal <c>``…``</c> span.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchInlineLiteral(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 4 || slice[0] is not (byte)'`' || slice[1] is not (byte)'`')
        {
            return 0;
        }

        const int OpenLength = 2;
        const int CloseLength = 2;
        var rest = slice[OpenLength..];
        var close = rest.IndexOf("``"u8);
        return close < 0 ? 0 : OpenLength + close + CloseLength;
    }

    /// <summary>Matches an interpreted-text span — single-backtick <c>`text`</c> with optional <c>_</c> reference trailer.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchInterpretedText(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'`')
        {
            return 0;
        }

        // Reject double-backtick (handled by inline-literal rule).
        if (slice.Length > 1 && slice[1] is (byte)'`')
        {
            return 0;
        }

        var rest = slice[1..];
        var close = rest.IndexOf((byte)'`');
        if (close < 0)
        {
            return 0;
        }

        var pos = 1 + close + 1;
        if (pos < slice.Length && slice[pos] is (byte)'_')
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Matches a strong-emphasis (<c>**bold**</c>) or emphasis (<c>*italic*</c>) run.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchStrongOrEmphasis(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'*')
        {
            return 0;
        }

        // **strong** — match a `*` then walk to a closing `**`.
        if (slice.Length > 1 && slice[1] is (byte)'*')
        {
            const int OpenLength = 2;
            const int CloseLength = 2;
            var rest = slice[OpenLength..];
            var close = rest.IndexOf("**"u8);
            return close < 0 ? 0 : OpenLength + close + CloseLength;
        }

        // *italic* — single asterisks. Reject empty runs (`**` already handled above).
        var inner = slice[1..];
        var endRel = inner.IndexOf((byte)'*');
        return endRel <= 0 ? 0 : 1 + endRel + 1;
    }

    /// <summary>Matches a substitution reference — <c>|name|</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchSubstitution(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchBracketedBlock(slice, (byte)'|', (byte)'|');

    /// <summary>Matches a standalone role marker — <c>:rolename:</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchRoleColon(ReadOnlySpan<byte> slice) =>
        MatchFieldName(slice);
}
