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
        [.. "abstract"u8],
        [.. "as"u8],
        [.. "assert"u8],
        [.. "base"u8],
        [.. "begin"u8],
        [.. "class"u8],
        [.. "default"u8],
        [.. "delegate"u8],
        [.. "do"u8],
        [.. "done"u8],
        [.. "downcast"u8],
        [.. "downto"u8],
        [.. "elif"u8],
        [.. "else"u8],
        [.. "end"u8],
        [.. "exception"u8],
        [.. "extern"u8],
        [.. "finally"u8],
        [.. "for"u8],
        [.. "function"u8],
        [.. "fun"u8],
        [.. "global"u8],
        [.. "if"u8],
        [.. "inherit"u8],
        [.. "inline"u8],
        [.. "interface"u8],
        [.. "internal"u8],
        [.. "in"u8],
        [.. "lazy"u8],
        [.. "let"u8],
        [.. "match"u8],
        [.. "member"u8],
        [.. "module"u8],
        [.. "mutable"u8],
        [.. "namespace"u8],
        [.. "new"u8],
        [.. "of"u8],
        [.. "open"u8],
        [.. "override"u8],
        [.. "private"u8],
        [.. "public"u8],
        [.. "rec"u8],
        [.. "return"u8],
        [.. "select"u8],
        [.. "static"u8],
        [.. "struct"u8],
        [.. "then"u8],
        [.. "to"u8],
        [.. "try"u8],
        [.. "type"u8],
        [.. "upcast"u8],
        [.. "use"u8],
        [.. "val"u8],
        [.. "void"u8],
        [.. "when"u8],
        [.. "while"u8],
        [.. "with"u8],
        [.. "yield"u8],
        [.. "atomic"u8],
        [.. "break"u8],
        [.. "checked"u8],
        [.. "component"u8],
        [.. "const"u8],
        [.. "constraint"u8],
        [.. "constructor"u8],
        [.. "continue"u8],
        [.. "eager"u8],
        [.. "event"u8],
        [.. "external"u8],
        [.. "fixed"u8],
        [.. "functor"u8],
        [.. "include"u8],
        [.. "method"u8],
        [.. "mixin"u8],
        [.. "object"u8],
        [.. "parallel"u8],
        [.. "process"u8],
        [.. "protected"u8],
        [.. "pure"u8],
        [.. "sealed"u8],
        [.. "tailcall"u8],
        [.. "trait"u8],
        [.. "virtual"u8],
        [.. "volatile"u8]);

    /// <summary>Boolean / null literal set.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create([.. "true"u8], [.. "false"u8], [.. "null"u8]);

    /// <summary>Built-in primitive type set.</summary>
    private static readonly ByteKeywordSet PrimitiveTypes = ByteKeywordSet.Create(
        [.. "sbyte"u8],
        [.. "byte"u8],
        [.. "char"u8],
        [.. "nativeint"u8],
        [.. "unativeint"u8],
        [.. "float32"u8],
        [.. "single"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "int8"u8],
        [.. "uint8"u8],
        [.. "int16"u8],
        [.. "uint16"u8],
        [.. "int32"u8],
        [.. "uint32"u8],
        [.. "int64"u8],
        [.. "uint64"u8],
        [.. "decimal"u8],
        [.. "unit"u8],
        [.. "bool"u8],
        [.. "string"u8],
        [.. "list"u8],
        [.. "exn"u8],
        [.. "obj"u8],
        [.. "enum"u8]);

    /// <summary>Word operators classified separately so themes can render <c>and</c>/<c>or</c>/<c>not</c> with the operator colour.</summary>
    private static readonly ByteKeywordSet WordOperators = ByteKeywordSet.Create([.."and"u8], [.."or"u8], [.."not"u8]);

    /// <summary>Operator alternation, sorted longest-first so multi-byte operators win before their single-byte prefixes.</summary>
    private static readonly byte[][] Operators =
    [
        [.. "<@@"u8], [.. "@@>"u8],
        [.. "<-"u8], [.. "->"u8], [.. "<="u8], [.. ">="u8],
        [.. "<>"u8], [.. "<|"u8], [.. "|>"u8], [.. "||"u8], [.. "&&"u8],
        [.. "::"u8], [.. ":="u8], [.. ":>"u8], [.. ":?"u8],
        [.. "<@"u8], [.. "@>"u8], [.. "<<"u8], [.. ">>"u8],
        [.. ".."u8], [.. "!="u8], [.. ";;"u8],
        [.. "["u8], [.. "]"u8], [.. "<"u8], [.. ">"u8],
        [.. "+"u8], [.. "-"u8], [.. "*"u8], [.. "/"u8],
        [.. "%"u8], [.. "&"u8], [.. "|"u8], [.. "^"u8],
        [.. "!"u8], [.. "~"u8], [.. "="u8], [.. "?"u8],
        [.. "@"u8]
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
                _ => 0
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

        if (matched < slice.Length && slice[matched] is (byte)'B')
        {
            return matched + 1;
        }

        return matched;
    }
}
