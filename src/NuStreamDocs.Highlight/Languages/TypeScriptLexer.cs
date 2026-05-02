// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>TypeScript lexer (also covers JavaScript via <see cref="JavaScriptLexer"/> reuse).</summary>
/// <remarks>
/// Modelled on Pygments' <c>TypeScriptLexer</c> shape. Single-state
/// machine — the language is regular enough that the cursor advances
/// past one token per cycle without ever stacking states. Template
/// literals and JSX are deliberately deferred; everything else
/// (strings, regex, comments, numbers, keywords, operators) lights
/// up.
/// </remarks>
public static class TypeScriptLexer
{
    /// <summary>Declaration-keyword set.</summary>
    private static readonly ByteKeywordSet DeclarationKeywords = ByteKeywordSet.Create(
        "var",
        "let",
        "const",
        "function",
        "class",
        "interface",
        "enum",
        "type",
        "namespace",
        "module",
        "export",
        "import",
        "from",
        "as",
        "declare",
        "abstract",
        "public",
        "private",
        "protected",
        "static",
        "async");

    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet GeneralKeywords = ByteKeywordSet.Create(
        "if",
        "else",
        "for",
        "while",
        "do",
        "return",
        "switch",
        "case",
        "default",
        "break",
        "continue",
        "throw",
        "try",
        "catch",
        "finally",
        "new",
        "delete",
        "in",
        "of",
        "instanceof",
        "typeof",
        "void",
        "yield",
        "await",
        "this",
        "super",
        "extends",
        "implements");

    /// <summary>Built-in TS type-keyword set.</summary>
    private static readonly ByteKeywordSet TypeKeywords = ByteKeywordSet.Create(
        "any",
        "boolean",
        "number",
        "string",
        "void",
        "never",
        "unknown",
        "object",
        "symbol",
        "bigint",
        "readonly",
        "keyof");

    /// <summary>Boolean / null / undefined / NaN / Infinity literal set.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        "true",
        "false",
        "null",
        "undefined",
        "NaN",
        "Infinity");

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] Operators =
    [
        "??="u8.ToArray(), "?."u8.ToArray(), "..."u8.ToArray(), "<<="u8.ToArray(), ">>>="u8.ToArray(), ">>="u8.ToArray(),
        "==="u8.ToArray(), "!=="u8.ToArray(), "&&="u8.ToArray(), "||="u8.ToArray(),
        "**="u8.ToArray(), "<="u8.ToArray(), ">="u8.ToArray(), "=="u8.ToArray(), "!="u8.ToArray(),
        "&&"u8.ToArray(), "||"u8.ToArray(), "++"u8.ToArray(), "--"u8.ToArray(), "<<"u8.ToArray(), ">>>"u8.ToArray(), ">>"u8.ToArray(),
        "+="u8.ToArray(), "-="u8.ToArray(), "*="u8.ToArray(), "/="u8.ToArray(), "%="u8.ToArray(), "&="u8.ToArray(), "|="u8.ToArray(), "^="u8.ToArray(),
        "**"u8.ToArray(), "??"u8.ToArray(), "=>"u8.ToArray(),
        "<"u8.ToArray(), ">"u8.ToArray(), "+"u8.ToArray(), "-"u8.ToArray(), "*"u8.ToArray(), "/"u8.ToArray(),
        "%"u8.ToArray(), "&"u8.ToArray(), "|"u8.ToArray(), "^"u8.ToArray(), "!"u8.ToArray(), "~"u8.ToArray(),
        "="u8.ToArray(), "?"u8.ToArray(),
    ];

    /// <summary>Identifier-continuation set: letters, digits, underscore, dollar.</summary>
    private static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_$"u8);

    /// <summary>Hex digit run with underscore separator.</summary>
    private static readonly SearchValues<byte> HexBody = SearchValues.Create("0123456789abcdefABCDEF_"u8);

    /// <summary>BigInt suffix — TS uses <c>n</c> on numeric literals to mark <c>BigInt</c>.</summary>
    private static readonly SearchValues<byte> BigintSuffix = SearchValues.Create("n"u8);

    /// <summary>First-byte set for backtick template literals.</summary>
    private static readonly SearchValues<byte> BacktickFirst = SearchValues.Create("`"u8);

    /// <summary>First-byte set for keyword constants.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfnuNI"u8);

    /// <summary>First-byte set for built-in type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abnsvuokr"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("vlcfietnmpsad"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("iefwdrscbtnovya"u8);

    /// <summary>First-byte set for identifiers (ASCII letters, underscore, dollar).</summary>
    private static readonly SearchValues<byte> IdentifierFirst = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$"u8);

    /// <summary>First-byte set for operator tokens.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("?=.<>!&|+-*/%^~"u8);

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the rule list. Exposed internal so <see cref="JavaScriptLexer"/> can reuse the patterns under a different language name.</summary>
    /// <param name="languageHint">Diagnostic-only language tag.</param>
    /// <returns>Ordered rule list.</returns>
    internal static LexerRule[] BuildRules(string languageHint)
    {
        _ = languageHint;
        return LanguageRuleBuilder.BuildCStyleRules(
            new(

                // [ \t\r\n]+ whitespace runs.
                new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.WhitespaceWithNewlinesFirst },

                // No doc-comment slot — TypeScript uses /** … */ JSDoc which is matched by the block-comment rule.
                null,

                // // line comment to end-of-line.
                new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst },

                // /* block comment */ — non-greedy.
                new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst },

                // No preprocessor slot — TS has no preprocessor directives.
                null,

                // `…` template-literal string with backslash escapes (interpolation expressions are not separately classified).
                new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'`'), TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = BacktickFirst },

                // "..." double-quoted string with backslash escapes.
                new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

                // '...' single-quoted string with backslash escapes.
                new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

                // No character-literal slot — TS doesn't distinguish a 'x' char literal from the single-quoted string above.
                null,

                // 0x[hex_]+n? hex literal with optional bigint suffix.
                new(
                    static slice => TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, BigintSuffix),
                    TokenClass.NumberHex,
                    LexerRule.NoStateChange) { FirstBytes = LanguageCommon.HexFirst },

                // \d+\.\d+([eE][+-]?\d+)? float literal — must precede the integer rule.
                new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DigitFirst },

                // [0-9_]+n? integer literal with optional bigint suffix.
                new(
                    static slice => TokenMatchers.MatchRunWithSuffix(slice, LanguageCommon.IntegerFirst, BigintSuffix),
                    TokenClass.NumberInteger,
                    LexerRule.NoStateChange) { FirstBytes = LanguageCommon.IntegerFirst },

                // true / false / null / undefined / NaN / Infinity literal.
                new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },

                // Built-in TS type keyword (any, boolean, number, string, void, never, unknown, ...).
                new(static slice => TokenMatchers.MatchKeyword(slice, TypeKeywords), TokenClass.KeywordType, LexerRule.NoStateChange) { FirstBytes = KeywordTypeFirst },

                // Declaration keyword (var, let, const, function, class, interface, enum, type, ...).
                new(
                    static slice => TokenMatchers.MatchKeyword(slice, DeclarationKeywords),
                    TokenClass.KeywordDeclaration,
                    LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst },

                // General keyword (if, for, await, throw, new, delete, instanceof, ...).
                new(static slice => TokenMatchers.MatchKeyword(slice, GeneralKeywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },

                // [A-Za-z_$][A-Za-z0-9_$]* identifier.
                new(
                    static slice => TokenMatchers.MatchIdentifier(slice, IdentifierFirst, IdentifierContinue),
                    TokenClass.Name,
                    LexerRule.NoStateChange) { FirstBytes = IdentifierFirst },

                // Operator alternation (longest-first): ??=, ?., ..., <<=, >>>=, >>=, ===, !==, ...
                new(static slice => TokenMatchers.MatchLongestLiteral(slice, Operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },

                // Single-byte C-curly punctuation: ( ) { } [ ] ; , . :
                new(
                    static slice => TokenMatchers.MatchSingleByteOf(slice, LanguageCommon.CCurlyPunctuationFirst),
                    TokenClass.Punctuation,
                    LexerRule.NoStateChange) { FirstBytes = LanguageCommon.CCurlyPunctuationFirst }));
    }

    /// <summary>Builds the lexer.</summary>
    /// <returns>Configured lexer.</returns>
    private static Lexer Build() => new("typescript", LanguageRuleBuilder.BuildSingleState(BuildRules("typescript")));
}
