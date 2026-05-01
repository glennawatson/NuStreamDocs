// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

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
    private static readonly FrozenSet<string> DeclarationKeywords = FrozenSet.ToFrozenSet(
        [
            "var", "let", "const", "function", "class", "interface", "enum", "type",
            "namespace", "module", "export", "import", "from", "as", "declare",
            "abstract", "public", "private", "protected", "static", "async",
        ],
        StringComparer.Ordinal);

    /// <summary>General-keyword set.</summary>
    private static readonly FrozenSet<string> GeneralKeywords = FrozenSet.ToFrozenSet(
        [
            "if", "else", "for", "while", "do", "return", "switch", "case", "default",
            "break", "continue", "throw", "try", "catch", "finally", "new", "delete",
            "in", "of", "instanceof", "typeof", "void", "yield", "await", "this",
            "super", "extends", "implements",
        ],
        StringComparer.Ordinal);

    /// <summary>Built-in TS type-keyword set.</summary>
    private static readonly FrozenSet<string> TypeKeywords = FrozenSet.ToFrozenSet(
        [
            "any", "boolean", "number", "string", "void", "never", "unknown", "object",
            "symbol", "bigint", "readonly", "keyof",
        ],
        StringComparer.Ordinal);

    /// <summary>Boolean / null / undefined / NaN / Infinity literal set.</summary>
    private static readonly FrozenSet<string> KeywordConstants = FrozenSet.ToFrozenSet(
        ["true", "false", "null", "undefined", "NaN", "Infinity"],
        StringComparer.Ordinal);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly string[] Operators =
    [
        "??=", "?.", "...", "<<=", ">>>=", ">>=", "===", "!==", "&&=", "||=",
        "**=", "<=", ">=", "==", "!=", "&&", "||", "++", "--", "<<", ">>>", ">>",
        "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "**", "??", "=>",
        "<", ">", "+", "-", "*", "/", "%", "&", "|", "^", "!", "~", "=", "?",
    ];

    /// <summary>Identifier-continuation set: letters, digits, underscore, dollar.</summary>
    private static readonly SearchValues<char> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_$");

    /// <summary>Hex digit run with underscore separator.</summary>
    private static readonly SearchValues<char> HexBody = SearchValues.Create("0123456789abcdefABCDEF_");

    /// <summary>BigInt suffix — TS uses <c>n</c> on numeric literals to mark <c>BigInt</c>.</summary>
    private static readonly SearchValues<char> BigintSuffix = SearchValues.Create("n");

    /// <summary>First-char set for backtick template literals.</summary>
    private static readonly SearchValues<char> BacktickFirst = SearchValues.Create("`");

    /// <summary>First-char set for keyword constants.</summary>
    private static readonly SearchValues<char> KeywordConstantFirst = SearchValues.Create("tfnuNI");

    /// <summary>First-char set for built-in type keywords.</summary>
    private static readonly SearchValues<char> KeywordTypeFirst = SearchValues.Create("abnsvuokr");

    /// <summary>First-char set for declaration keywords.</summary>
    private static readonly SearchValues<char> KeywordDeclarationFirst = SearchValues.Create("vlcfietnmpsad");

    /// <summary>First-char set for general keywords.</summary>
    private static readonly SearchValues<char> KeywordFirst = SearchValues.Create("iefwdrscbtnovya");

    /// <summary>First-char set for identifiers (ASCII letters, underscore, dollar).</summary>
    private static readonly SearchValues<char> IdentifierFirst = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$");

    /// <summary>First-char set for operator tokens.</summary>
    private static readonly SearchValues<char> OperatorFirst = SearchValues.Create("?=.<>!&|+-*/%^~");

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
                new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, NextState: null) { FirstChars = LanguageCommon.WhitespaceWithNewlinesFirst },

                // No doc-comment slot — TypeScript uses /** … */ JSDoc which is matched by the block-comment rule.
                null,

                // // line comment to end-of-line.
                new(LanguageCommon.LineComment, TokenClass.CommentSingle, NextState: null) { FirstChars = LanguageCommon.SlashFirst },

                // /* block comment */ — non-greedy.
                new(LanguageCommon.BlockComment, TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.SlashFirst },

                // No preprocessor slot — TS has no preprocessor directives.
                null,

                // `…` template-literal string with backslash escapes (interpolation expressions are not separately classified).
                new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, '`'), TokenClass.StringDouble, NextState: null) { FirstChars = BacktickFirst },

                // "..." double-quoted string with backslash escapes.
                new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst },

                // '...' single-quoted string with backslash escapes.
                new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, '\''), TokenClass.StringSingle, NextState: null) { FirstChars = LanguageCommon.SingleQuoteFirst },

                // No character-literal slot — TS doesn't distinguish a 'x' char literal from the single-quoted string above.
                null,

                // 0x[hex_]+n? hex literal with optional bigint suffix.
                new(
                    static slice => TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, BigintSuffix),
                    TokenClass.NumberHex,
                    NextState: null) { FirstChars = LanguageCommon.HexFirst },

                // \d+\.\d+([eE][+-]?\d+)? float literal — must precede the integer rule.
                new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, NextState: null) { FirstChars = LanguageCommon.DigitFirst },

                // [0-9_]+n? integer literal with optional bigint suffix.
                new(
                    static slice => TokenMatchers.MatchRunWithSuffix(slice, LanguageCommon.IntegerFirst, BigintSuffix),
                    TokenClass.NumberInteger,
                    NextState: null) { FirstChars = LanguageCommon.IntegerFirst },

                // true / false / null / undefined / NaN / Infinity literal.
                new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, NextState: null) { FirstChars = KeywordConstantFirst },

                // Built-in TS type keyword (any, boolean, number, string, void, never, unknown, ...).
                new(static slice => TokenMatchers.MatchKeyword(slice, TypeKeywords), TokenClass.KeywordType, NextState: null) { FirstChars = KeywordTypeFirst },

                // Declaration keyword (var, let, const, function, class, interface, enum, type, ...).
                new(
                    static slice => TokenMatchers.MatchKeyword(slice, DeclarationKeywords),
                    TokenClass.KeywordDeclaration,
                    NextState: null) { FirstChars = KeywordDeclarationFirst },

                // General keyword (if, for, await, throw, new, delete, instanceof, ...).
                new(static slice => TokenMatchers.MatchKeyword(slice, GeneralKeywords), TokenClass.Keyword, NextState: null) { FirstChars = KeywordFirst },

                // [A-Za-z_$][A-Za-z0-9_$]* identifier.
                new(
                    static slice => TokenMatchers.MatchIdentifier(slice, IdentifierFirst, IdentifierContinue),
                    TokenClass.Name,
                    NextState: null) { FirstChars = IdentifierFirst },

                // Operator alternation (longest-first): ??=, ?., ..., <<=, >>>=, >>=, ===, !==, ...
                new(static slice => TokenMatchers.MatchLongestLiteral(slice, Operators), TokenClass.Operator, NextState: null) { FirstChars = OperatorFirst },

                // Single-character C-curly punctuation: ( ) { } [ ] ; , . :
                new(
                    static slice => TokenMatchers.MatchSingleCharOf(slice, LanguageCommon.CCurlyPunctuationFirst),
                    TokenClass.Punctuation,
                    NextState: null) { FirstChars = LanguageCommon.CCurlyPunctuationFirst }));
    }

    /// <summary>Builds the lexer.</summary>
    /// <returns>Configured lexer.</returns>
    private static Lexer Build() => new("typescript", LanguageRuleBuilder.BuildSingleState(BuildRules("typescript")));
}
