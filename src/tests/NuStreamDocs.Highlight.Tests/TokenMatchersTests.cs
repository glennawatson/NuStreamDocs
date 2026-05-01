// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Theory-style tests for the <see cref="TokenMatchers"/> primitives shared across all lexers.</summary>
public class TokenMatchersTests
{
    /// <summary>Hex body matches the C# / TS hex-digit body including underscore separators.</summary>
    private static readonly SearchValues<char> HexBody = SearchValues.Create("0123456789abcdefABCDEF_");

    /// <summary>Integer body — digits with optional <c>_</c> separators.</summary>
    private static readonly SearchValues<char> IntegerBody = SearchValues.Create("0123456789_");

    /// <summary>C#-shape integer suffix.</summary>
    private static readonly SearchValues<char> IntegerSuffix = SearchValues.Create("uUlL");

    /// <summary>YAML anchor / alias body.</summary>
    private static readonly SearchValues<char> AnchorBody = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-");

    /// <summary>Markup text terminators — <c>&lt;</c> and <c>&amp;</c>.</summary>
    private static readonly SearchValues<char> MarkupTextStop = SearchValues.Create("<&");

    /// <summary>JSON's three keyword constants.</summary>
    private static readonly FrozenSet<string> JsonKeywords = FrozenSet.ToFrozenSet(
        ["true", "false", "null"],
        StringComparer.Ordinal);

    /// <summary>Case-insensitive YAML literal constants.</summary>
    private static readonly FrozenSet<string> YamlKeywords = FrozenSet.ToFrozenSet(
        ["true", "false", "null", "yes", "no", "on", "off"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Operators ordered longest-first so the alternation prefers the longer match.</summary>
    private static readonly string[] ShortOperators = ["==", "=>", "="];

    /// <summary>ASCII whitespace runs match every contiguous space / tab / newline at the cursor.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("   abc", 3)]
    [Arguments("\t\nfoo", 2)]
    [Arguments(" \t \r\n!", 5)]
    [Arguments("xyz", 0)]
    [Arguments("", 0)]
    public async Task MatchAsciiWhitespace_returns_run_length(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchAsciiWhitespace(input)).IsEqualTo(expected);

    /// <summary>Inline whitespace excludes line terminators.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("  \t code", 4)]
    [Arguments("  \nline2", 2)]
    [Arguments("\r\n", 0)]
    [Arguments("nope", 0)]
    public async Task MatchAsciiInlineWhitespace_excludes_newlines(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchAsciiInlineWhitespace(input)).IsEqualTo(expected);

    /// <summary>Hash-prefixed line comments stop at the first <c>\r</c> or <c>\n</c>.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("# trailing comment", 18)]
    [Arguments("#shorthand\nrest", 10)]
    [Arguments("# windows\r\nrest", 9)]
    [Arguments("# only comment", 14)]
    [Arguments("not a comment", 0)]
    public async Task MatchHashComment_consumes_to_eol(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchHashComment(input)).IsEqualTo(expected);

    /// <summary>ASCII identifier matches a letter-or-underscore start followed by letters / digits / underscores.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("foo bar", 3)]
    [Arguments("_under123 ", 9)]
    [Arguments("Camel_Case_42", 13)]
    [Arguments("123leadingDigit", 0)]
    [Arguments(".dot", 0)]
    public async Task MatchAsciiIdentifier_returns_identifier_length(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchAsciiIdentifier(input)).IsEqualTo(expected);

    /// <summary>Signed integer matches optional <c>-</c> and one or more digits.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("123", 3)]
    [Arguments("-42 next", 3)]
    [Arguments("-", 0)]
    [Arguments("- 7", 0)]
    [Arguments("abc", 0)]
    public async Task MatchSignedAsciiInteger_handles_sign(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchSignedAsciiInteger(input)).IsEqualTo(expected);

    /// <summary>Signed float requires at least one digit on each side of the dot; exponent and sign are optional.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("1.0", 3)]
    [Arguments("-3.14 next", 5)]
    [Arguments("2.5e10", 6)]
    [Arguments("1.0E+3", 6)]
    [Arguments("6.02e-23", 8)]
    [Arguments("1.", 0)]
    [Arguments(".5", 0)]
    [Arguments("123", 0)]
    public async Task MatchSignedAsciiFloat_handles_exponent(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchSignedAsciiFloat(input)).IsEqualTo(expected);

    /// <summary>Unsigned float rejects leading minus.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("1.0", 3)]
    [Arguments("-1.0", 0)]
    [Arguments("0.5e2", 5)]
    public async Task MatchUnsignedAsciiFloat_rejects_minus(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchUnsignedAsciiFloat(input)).IsEqualTo(expected);

    /// <summary>Single-quoted no-escape strings consume through the closing quote.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("'hello' world", 7)]
    [Arguments("''", 2)]
    [Arguments("'unterminated", 0)]
    [Arguments("not quoted", 0)]
    [Arguments("\"different\"", 0)]
    public async Task MatchSingleQuotedNoEscape_handles_closing_quote(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchSingleQuotedNoEscape(input)).IsEqualTo(expected);

    /// <summary>Double-quoted with backslash escape recognises <c>\"</c> as an embedded quote.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("\"hello\"", 7)]
    [Arguments("\"a\\\"b\" tail", 6)]
    [Arguments("\"unterminated", 0)]
    [Arguments("'wrong quote'", 0)]
    public async Task MatchDoubleQuotedWithBackslashEscape_treats_backslash_quote_as_literal(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchDoubleQuotedWithBackslashEscape(input)).IsEqualTo(expected);

    /// <summary>Doubled-quote escape (SQL/Pascal/YAML style) treats <c>''</c> as a literal apostrophe.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("'simple'", 8)]
    [Arguments("'it''s' tail", 7)]
    [Arguments("''", 2)]
    [Arguments("'unterminated", 0)]
    public async Task MatchSingleQuotedDoubledEscape_handles_doubled_quote(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchSingleQuotedDoubledEscape(input)).IsEqualTo(expected);

    /// <summary>Bracketed block consumes <c>open</c> + body + <c>close</c>.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="open">Opening character.</param>
    /// <param name="close">Closing character.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("{abc} tail", '{', '}', 5)]
    [Arguments("[1,2,3]", '[', ']', 7)]
    [Arguments("(empty)", '(', ')', 7)]
    [Arguments("{} tail", '{', '}', 2)]
    [Arguments("{unterminated", '{', '}', 0)]
    [Arguments("no opener", '{', '}', 0)]
    public async Task MatchBracketedBlock_consumes_paired_delimiters(string input, char open, char close, int expected) =>
        await Assert.That(TokenMatchers.MatchBracketedBlock(input, open, close)).IsEqualTo(expected);

    /// <summary>Newline matcher recognises CRLF, lone CR, and lone LF.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("\r\nrest", 2)]
    [Arguments("\nrest", 1)]
    [Arguments("\rrest", 1)]
    [Arguments("notnewline", 0)]
    [Arguments("", 0)]
    public async Task MatchNewline_handles_all_line_terminators(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchNewline(input)).IsEqualTo(expected);

    /// <summary>Delimited block matches non-greedy <c>open ... close</c>.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("<!-- hi --> rest", 11)]
    [Arguments("<!-- early --> later -->", 14)]
    [Arguments("<!-- unterminated", 0)]
    [Arguments("not a comment", 0)]
    public async Task MatchDelimited_html_comment(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchDelimited(input, "<!--", "-->")).IsEqualTo(expected);

    /// <summary>Run-until-any consumes everything up to (but not including) the first character in the stop set.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("plain text<tag>", 10)]
    [Arguments("starts<at zero", 6)]
    [Arguments("<immediate", 0)]
    [Arguments("no stops here", 13)]
    public async Task MatchRunUntilAny_html_text(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchRunUntilAny(input, MarkupTextStop)).IsEqualTo(expected);

    /// <summary>Hex literal matcher recognises <c>0x</c>/<c>0X</c> + body + optional suffix.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("0xDEAD_BEEFL", 12)]
    [Arguments("0X1A2B", 6)]
    [Arguments("0xff", 4)]
    [Arguments("0x", 0)]
    [Arguments("123", 0)]
    public async Task MatchAsciiHexLiteral_with_csharp_suffixes(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchAsciiHexLiteral(input, HexBody, IntegerSuffix)).IsEqualTo(expected);

    /// <summary>Run-with-suffix matches body + optional trailing chars.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("123uL", 5)]
    [Arguments("0_000UL", 7)]
    [Arguments("100", 3)]
    [Arguments("abc", 0)]
    public async Task MatchRunWithSuffix_csharp_integer_shape(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchRunWithSuffix(input, IntegerBody, IntegerSuffix)).IsEqualTo(expected);

    /// <summary>Prefixed-run matches a single prefix character + a non-empty body.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("&anchor1 rest", 8)]
    [Arguments("&a", 2)]
    [Arguments("&", 0)]
    [Arguments("&!", 0)]
    [Arguments("noprefix", 0)]
    public async Task MatchPrefixedRun_anchor_shape(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchPrefixedRun(input, '&', AnchorBody)).IsEqualTo(expected);

    /// <summary>Keyword matcher accepts an exact match against a member of the keyword set with a non-identifier-continue boundary.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("true ", 4)]
    [Arguments("false)", 5)]
    [Arguments("null,", 4)]
    [Arguments("truthy", 0)]
    [Arguments("nu", 0)]
    [Arguments("trueish", 0)]
    public async Task MatchKeyword_word_boundary(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchKeyword(input, JsonKeywords)).IsEqualTo(expected);

    /// <summary>Keyword (case-insensitive) recognises any case combination.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("True ", 4)]
    [Arguments("FALSE)", 5)]
    [Arguments("Yes,", 3)]
    [Arguments("notakeyword", 0)]
    public async Task MatchKeywordIgnoreCase_case_insensitive(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchKeywordIgnoreCase(input, YamlKeywords)).IsEqualTo(expected);

    /// <summary>Longest-literal alternation picks the longest matching prefix.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("==>tail", 2)]
    [Arguments("=> arrow", 2)]
    [Arguments("=expr", 1)]
    [Arguments("nope", 0)]
    public async Task MatchLongestLiteral_prefers_longer_match(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchLongestLiteral(input, ShortOperators)).IsEqualTo(expected);

    /// <summary>Double-quoted-key recognises a quoted string followed by optional whitespace and <c>:</c>.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("\"key\":42", 5)]
    [Arguments("\"key\" : 42", 5)]
    [Arguments("\"value\",", 0)]
    [Arguments("\"unterminated", 0)]
    public async Task MatchDoubleQuotedKey_requires_colon_lookahead(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchDoubleQuotedKey(input)).IsEqualTo(expected);

    /// <summary>Raw quoted strings honour the matched-opener-count rule — N opening quotes match exactly N closing quotes.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="minQuotes">Minimum opening / closing quote run.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("\"\"\"hello\"\"\"", 3, 11)]
    [Arguments("\"\"\"\"five\"\"\"\"\"", 4, 13)]
    [Arguments("\"\"\"with \"\"quote inside\"\"\"", 3, 25)]
    [Arguments("\"\"\"unterminated", 3, 0)]
    [Arguments("\"\"only-two-quotes\"\"", 3, 0)]
    [Arguments("not raw", 3, 0)]
    public async Task MatchRawQuotedString_handles_variable_quote_count(string input, int minQuotes, int expected) =>
        await Assert.That(TokenMatchers.MatchRawQuotedString(input, '"', minQuotes)).IsEqualTo(expected);

    /// <summary>Multi-line raw strings consume newlines without breaking the match.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async Task MatchRawQuotedString_spans_newlines()
    {
        const string Source = "\"\"\"\n  multi\n  line\n  \"\"\"";
        await Assert.That(TokenMatchers.MatchRawQuotedString(Source, '"', 3)).IsEqualTo(Source.Length);
    }

    /// <summary>Doubled-quote-escape variant for double quotes (<c>"a""b"</c> Pascal/SQL style).</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("\"plain\"", 7)]
    [Arguments("\"a\"\"b\" tail", 6)]
    [Arguments("\"\"", 2)]
    [Arguments("\"unterminated", 0)]
    [Arguments("not quoted", 0)]
    public async Task MatchDoubleQuotedDoubledEscape_handles_doubled_quote(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchDoubleQuotedDoubledEscape(input)).IsEqualTo(expected);

    /// <summary>Unsigned float matches positive floats only (no leading sign accepted).</summary>
    /// <param name="input">Input string.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("3.14", 4)]
    [Arguments("0.5e10", 6)]
    [Arguments("-3.14", 0)]
    [Arguments("+3.14", 0)]
    [Arguments("3.", 0)]
    public async Task MatchUnsignedAsciiFloat_rejects_signed_input(string input, int expected) =>
        await Assert.That(TokenMatchers.MatchUnsignedAsciiFloat(input)).IsEqualTo(expected);

    /// <summary>Match-literal returns the literal length on a starts-with match, 0 otherwise.</summary>
    /// <param name="input">Input string.</param>
    /// <param name="literal">Literal to match.</param>
    /// <param name="expected">Expected matched length.</param>
    /// <returns>Async task.</returns>
    [Test]
    [Arguments("if ", "if", 2)]
    [Arguments("ifelse", "if", 2)]
    [Arguments("else", "if", 0)]
    [Arguments("", "if", 0)]
    [Arguments("if", "if", 2)]
    public async Task MatchLiteral_starts_with_check(string input, string literal, int expected) =>
        await Assert.That(TokenMatchers.MatchLiteral(input, literal)).IsEqualTo(expected);
}
