// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>F# rule list factory.</summary>
/// <remarks>
/// Pragmatic single-state port of Pygments'
/// <c>FSharpLexer</c> (<c>pygments/lexers/dotnet.py</c>). The Pygments
/// shape is multi-state — separate states for triple-quoted strings,
/// block comments (with nesting), and dotted-path lookups. We collapse
/// to one state by:
/// <list type="bullet">
/// <item><description>Triple-quoted <c>"""..."""</c> consumed as a single token via a
///   dedicated bytewise matcher (newlines included).</description></item>
/// <item><description>Block comments <c>(* ... *)</c> matched flat (no nesting). F#
///   allows nesting, but the byte-level lexer is a syntax-highlight
///   approximation; nested block comments degrade gracefully — outer
///   delimiters still classify, inner content still reads.</description></item>
/// <item><description>Verbatim <c>@"..."</c> uses the same doubled-quote escape as C#.</description></item>
/// </list>
/// </remarks>
internal static class FSharpRules
{
    /// <summary>Length of the <c>///</c> doc-comment introducer.</summary>
    private const int DocCommentPrefixLength = 3;

    /// <summary>Length of a basic <c>'x'</c> character literal.</summary>
    private const int BasicCharLiteralLength = 3;

    /// <summary>Length of an escaped <c>'\x'</c> character literal.</summary>
    private const int EscapedCharLiteralLength = 4;

    /// <summary>Minimum opening / closing quote run for a triple-quoted string literal (<c>"""</c>).</summary>
    private const int TripleQuoteLength = 3;

    /// <summary>Length of the <c>(*</c> block-comment opener.</summary>
    private const int BlockCommentDelimiterLength = 2;

    /// <summary>Control-flow / declaration keywords plus reserved words; pygments classifies the reserved set as keywords too.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        "abstract",
        "as",
        "assert",
        "base",
        "begin",
        "class",
        "default",
        "delegate",
        "do",
        "done",
        "downcast",
        "downto",
        "elif",
        "else",
        "end",
        "exception",
        "extern",
        "finally",
        "for",
        "function",
        "fun",
        "global",
        "if",
        "inherit",
        "inline",
        "interface",
        "internal",
        "in",
        "lazy",
        "let",
        "match",
        "member",
        "module",
        "mutable",
        "namespace",
        "new",
        "of",
        "open",
        "override",
        "private",
        "public",
        "rec",
        "return",
        "select",
        "static",
        "struct",
        "then",
        "to",
        "try",
        "type",
        "upcast",
        "use",
        "val",
        "void",
        "when",
        "while",
        "with",
        "yield",
        "atomic",
        "break",
        "checked",
        "component",
        "const",
        "constraint",
        "constructor",
        "continue",
        "eager",
        "event",
        "external",
        "fixed",
        "functor",
        "include",
        "method",
        "mixin",
        "object",
        "parallel",
        "process",
        "protected",
        "pure",
        "sealed",
        "tailcall",
        "trait",
        "virtual",
        "volatile");

    /// <summary>Boolean / null literal set.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create("true", "false", "null");

    /// <summary>Built-in primitive type set.</summary>
    private static readonly ByteKeywordSet PrimitiveTypes = ByteKeywordSet.Create(
        "sbyte",
        "byte",
        "char",
        "nativeint",
        "unativeint",
        "float32",
        "single",
        "float",
        "double",
        "int8",
        "uint8",
        "int16",
        "uint16",
        "int32",
        "uint32",
        "int64",
        "uint64",
        "decimal",
        "unit",
        "bool",
        "string",
        "list",
        "exn",
        "obj",
        "enum");

    /// <summary>Word operators classified separately so themes can render <c>and</c>/<c>or</c>/<c>not</c> with the operator colour.</summary>
    private static readonly ByteKeywordSet WordOperators = ByteKeywordSet.Create("and", "or", "not");

    /// <summary>Operator alternation, sorted longest-first so multi-byte operators win before their single-byte prefixes.</summary>
    private static readonly byte[][] Operators =
    [
        "<@@"u8.ToArray(), "@@>"u8.ToArray(),
        "<-"u8.ToArray(), "->"u8.ToArray(), "<="u8.ToArray(), ">="u8.ToArray(),
        "<>"u8.ToArray(), "<|"u8.ToArray(), "|>"u8.ToArray(), "||"u8.ToArray(), "&&"u8.ToArray(),
        "::"u8.ToArray(), ":="u8.ToArray(), ":>"u8.ToArray(), ":?"u8.ToArray(),
        "<@"u8.ToArray(), "@>"u8.ToArray(), "<<"u8.ToArray(), ">>"u8.ToArray(),
        ".."u8.ToArray(), "!="u8.ToArray(), ";;"u8.ToArray(),
        "["u8.ToArray(), "]"u8.ToArray(), "<"u8.ToArray(), ">"u8.ToArray(),
        "+"u8.ToArray(), "-"u8.ToArray(), "*"u8.ToArray(), "/"u8.ToArray(),
        "%"u8.ToArray(), "&"u8.ToArray(), "|"u8.ToArray(), "^"u8.ToArray(),
        "!"u8.ToArray(), "~"u8.ToArray(), "="u8.ToArray(), "?"u8.ToArray(),
        "@"u8.ToArray(),
    ];

    /// <summary>Integer suffix bytes — F# spec § 4.5: y/uy/s/us/l/u/L/UL/n/un/Q/R/Z/I/N/G/m/M.</summary>
    private static readonly SearchValues<byte> IntegerSuffix = SearchValues.Create("yslLnQRZINGmMuU"u8);

    /// <summary>Float suffix bytes — <c>f</c> (single), <c>F</c>, <c>m</c>/<c>M</c> (decimal).</summary>
    private static readonly SearchValues<byte> FloatSuffix = SearchValues.Create("fFmM"u8);

    /// <summary>Hex-digit run continuation: 0-9 a-f A-F plus underscore separator.</summary>
    private static readonly SearchValues<byte> HexBody = SearchValues.Create("0123456789abcdefABCDEF_"u8);

    /// <summary>First-byte set for whitespace runs (newlines included so the lexer doesn't stall on blank lines inside a script).</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the boolean / null literals.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for the primitive type keywords.</summary>
    private static readonly SearchValues<byte> PrimitiveTypeFirst = SearchValues.Create("sbcunfduiledol"u8);

    /// <summary>First-byte set for the language keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("aAbBcCdDeEfFgGhHiIlLmMnNoOpPqQrRsStTuUvVwWyYxX"u8);

    /// <summary>First-byte set for the word operators.</summary>
    private static readonly SearchValues<byte> WordOperatorFirst = SearchValues.Create("anor"u8);

    /// <summary>First-byte set for operator tokens — every byte that may begin a recognized operator.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("<>=!&|+-*/%^~?:@.;[]"u8);

    /// <summary>First-byte set for preprocessor directive (<c>#</c> at line start).</summary>
    private static readonly SearchValues<byte> PreprocessorFirst = SearchValues.Create(" \t#"u8);

    /// <summary>F#-specific punctuation set — comma, dot, parens, braces.</summary>
    private static readonly SearchValues<byte> Punctuation = SearchValues.Create("(){},."u8);

    /// <summary>First-byte set for the block-comment opener (<c>(*</c>).</summary>
    private static readonly SearchValues<byte> ParenFirst = SearchValues.Create("("u8);

    /// <summary>First-byte set for triple-quote / regular / verbatim string introducers.</summary>
    private static readonly SearchValues<byte> StringFirst = SearchValues.Create("\"@"u8);

    /// <summary>Identifier-continuation set including the F# trailing-apostrophe convention.</summary>
    private static readonly SearchValues<byte> FSharpIdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_'"u8);

    /// <summary>Builds the F# root-state rule list.</summary>
    /// <returns>Ordered rule list.</returns>
    public static LexerRule[] Build() =>
        BuildRules();

    /// <summary>Constructs the F# rule list — order matters: longer/more-specific rules precede their substring counterparts.</summary>
    /// <returns>Ordered rule list.</returns>
    private static LexerRule[] BuildRules()
    {
        var rules = new LexerRule[16];
        var i = 0;

        // Whitespace including newlines — F# scripts span multiple lines.
        rules[i++] = new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst };

        // /// xml-doc-comment to end-of-line — must precede the line-comment rule.
        rules[i++] = new(
            static slice => slice is [(byte)'/', (byte)'/', (byte)'/', ..] ? DocCommentPrefixLength + TokenMatchers.LineLength(slice[DocCommentPrefixLength..]) : 0,
            TokenClass.CommentSpecial,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

        // // line comment to end-of-line.
        rules[i++] = new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

        // (* block comment *) — flat match (nesting degrades gracefully).
        rules[i++] = new(MatchBlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = ParenFirst };

        // # preprocessor directive — line-anchored (#if / #endif / #else / #line / #nowarn / #light).
        rules[i++] = new(LanguageCommon.MatchHashPreprocessor, TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = PreprocessorFirst, RequiresLineStart = true };

        // """..."""B? triple-quoted string — must precede regular and verbatim strings.
        rules[i++] = new(MatchTripleQuotedString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst };

        // @"..." verbatim string with "" as embedded-quote escape.
        rules[i++] = new(LanguageCommon.MatchVerbatimString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AtFirst };

        // "..."B? regular double-quoted string with backslash escapes.
        rules[i++] = new(MatchRegularString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = StringFirst };

        // 'x' or '\x' single-character literal.
        rules[i++] = new(
            static slice => slice switch
            {
                [(byte)'\'', (byte)'\\', _, (byte)'\'', ..] => EscapedCharLiteralLength,
                [(byte)'\'', _, (byte)'\'', ..] => BasicCharLiteralLength,
                _ => 0,
            },
            TokenClass.StringSingle,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst };

        // 0x[hex_]+[suffix]* hex integer literal — must precede the integer rule.
        rules[i++] = new(
            static slice => TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, IntegerSuffix),
            TokenClass.NumberHex,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.HexFirst };

        // [0-9]+\.[0-9]+([eE][+-]?[0-9]+)?[fFmM]? float literal — must precede the integer rule.
        rules[i++] = new(static slice => LanguageCommon.MatchFloatWithOptionalSuffix(slice, FloatSuffix), TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DigitFirst };

        // [0-9_]+[suffix]* integer literal.
        rules[i++] = new(
            static slice => TokenMatchers.MatchRunWithSuffix(slice, LanguageCommon.IntegerFirst, IntegerSuffix),
            TokenClass.NumberInteger,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.IntegerFirst };

        // Word operator (and / or / not) — must precede the keyword and identifier rules.
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, WordOperators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = WordOperatorFirst };

        // true / false / null literal — must precede the general keyword and primitive-type rules.
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst };

        // Built-in primitive type keyword (int / string / bool / list / ...).
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, PrimitiveTypes), TokenClass.KeywordType, LexerRule.NoStateChange) { FirstBytes = PrimitiveTypeFirst };

        // General keyword (let / fun / match / module / ... + reserved words).
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst };

        return AppendIdentifierAndPunctuation(rules, i);
    }

    /// <summary>Appends the identifier, operator, and punctuation rules to <paramref name="rules"/> starting at <paramref name="written"/>.</summary>
    /// <param name="rules">Pre-sized rule array.</param>
    /// <param name="written">Number of rules already populated.</param>
    /// <returns>The rule array with all entries populated.</returns>
    /// <remarks>
    /// Split out so <see cref="BuildRules"/> stays under the project's
    /// per-method line / cyclomatic-complexity caps without burying the
    /// rule order in nested helper calls.
    /// </remarks>
    private static LexerRule[] AppendIdentifierAndPunctuation(LexerRule[] rules, int written)
    {
        var grown = new LexerRule[written + 3];
        Array.Copy(rules, 0, grown, 0, written);
        var i = written;

        // [A-Za-z_][A-Za-z0-9_']* identifier — F# allows trailing apostrophes (`x'`).
        grown[i++] = new(
            static slice => TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, FSharpIdentifierContinue),
            TokenClass.Name,
            LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart };

        // Operator alternation (longest-first).
        grown[i++] = new(
            static slice => TokenMatchers.MatchLongestLiteral(slice, Operators),
            TokenClass.Operator,
            LexerRule.NoStateChange) { FirstBytes = OperatorFirst };

        // F# punctuation: ( ) { } , . — semicolons are operators in F# (`;;` is the FSI terminator).
        grown[i] = new(
            static slice => TokenMatchers.MatchSingleByteOf(slice, Punctuation),
            TokenClass.Punctuation,
            LexerRule.NoStateChange) { FirstBytes = Punctuation };

        return grown;
    }

    /// <summary>F# block comment <c>(* ... *)</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    /// <remarks>Flat match — the spec allows nesting, but at the byte-level lexer layer a flat scan reads the surface form correctly and keeps complexity low.</remarks>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < BlockCommentDelimiterLength + BlockCommentDelimiterLength
            || slice[0] is not (byte)'(' || slice[1] is not (byte)'*')
        {
            return 0;
        }

        var rest = slice[BlockCommentDelimiterLength..];
        var close = rest.IndexOf("*)"u8);
        return close < 0 ? 0 : BlockCommentDelimiterLength + close + BlockCommentDelimiterLength;
    }

    /// <summary>Triple-quoted string <c>"""..."""</c>; spans newlines and accepts an optional <c>B</c> bytestring suffix.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchTripleQuotedString(ReadOnlySpan<byte> slice)
    {
        if (slice is not [(byte)'"', (byte)'"', (byte)'"', ..])
        {
            return 0;
        }

        var rest = slice[TripleQuoteLength..];
        var close = rest.IndexOf("\"\"\""u8);
        if (close < 0)
        {
            return 0;
        }

        var matched = TripleQuoteLength + close + TripleQuoteLength;
        return matched < slice.Length && slice[matched] is (byte)'B' ? matched + 1 : matched;
    }

    /// <summary>Regular double-quoted string with backslash escapes; accepts an optional <c>B</c> bytestring suffix.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchRegularString(ReadOnlySpan<byte> slice)
    {
        var matched = TokenMatchers.MatchDoubleQuotedWithBackslashEscape(slice);
        if (matched is 0)
        {
            return 0;
        }

        return matched < slice.Length && slice[matched] is (byte)'B' ? matched + 1 : matched;
    }
}
