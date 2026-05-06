// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Scripting;

/// <summary>TypeScript lexer (also covers JavaScript via <see cref="JavaScriptLexer"/> reuse).</summary>
/// <remarks>
/// Single-state machine — the language is regular enough that the cursor advances
/// past one token per cycle without ever stacking states. Template literals and JSX
/// are deliberately deferred; everything else (strings, regex, comments, numbers,
/// keywords, operators) lights up.
/// </remarks>
public static class TypeScriptLexer
{
    /// <summary>Declaration-keyword set.</summary>
    private static readonly ByteKeywordSet DeclarationKeywords = ByteKeywordSet.Create(
        [.. "var"u8],
        [.. "let"u8],
        [.. "const"u8],
        [.. "function"u8],
        [.. "class"u8],
        [.. "interface"u8],
        [.. "enum"u8],
        [.. "type"u8],
        [.. "namespace"u8],
        [.. "module"u8],
        [.. "export"u8],
        [.. "import"u8],
        [.. "from"u8],
        [.. "as"u8],
        [.. "declare"u8],
        [.. "abstract"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "static"u8],
        [.. "async"u8]);

    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet GeneralKeywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "else"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "return"u8],
        [.. "switch"u8],
        [.. "case"u8],
        [.. "default"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "new"u8],
        [.. "delete"u8],
        [.. "in"u8],
        [.. "of"u8],
        [.. "instanceof"u8],
        [.. "typeof"u8],
        [.. "void"u8],
        [.. "yield"u8],
        [.. "await"u8],
        [.. "this"u8],
        [.. "super"u8],
        [.. "extends"u8],
        [.. "implements"u8]);

    /// <summary>Built-in TS type-keyword set.</summary>
    private static readonly ByteKeywordSet TypeKeywords = ByteKeywordSet.Create(
        [.. "any"u8],
        [.. "boolean"u8],
        [.. "number"u8],
        [.. "string"u8],
        [.. "void"u8],
        [.. "never"u8],
        [.. "unknown"u8],
        [.. "object"u8],
        [.. "symbol"u8],
        [.. "bigint"u8],
        [.. "readonly"u8],
        [.. "keyof"u8]);

    /// <summary>Boolean / null / undefined / NaN / Infinity literal set.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8],
        [.. "undefined"u8],
        [.. "NaN"u8],
        [.. "Infinity"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "??="u8],
        [.. "?."u8],
        [.. "..."u8],
        [.. "<<="u8],
        [.. ">>>="u8],
        [.. ">>="u8],
        [.. "==="u8],
        [.. "!=="u8],
        [.. "&&="u8],
        [.. "||="u8],
        [.. "**="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "++"u8],
        [.. "--"u8],
        [.. "<<"u8],
        [.. ">>>"u8],
        [.. ">>"u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
        [.. "&="u8],
        [.. "|="u8],
        [.. "^="u8],
        [.. "**"u8],
        [.. "??"u8],
        [.. "=>"u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "&"u8],
        [.. "|"u8],
        [.. "^"u8],
        [.. "!"u8],
        [.. "~"u8],
        [.. "="u8],
        [.. "?"u8]
    ];

    /// <summary>Identifier-start byte set: ASCII letters, underscore, dollar (per the JS spec's identifier rules, restricted to ASCII).</summary>
    private static readonly SearchValues<byte> IdentifierFirst = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$"u8);

    /// <summary>Identifier-continuation byte set: letters, digits, underscore, dollar.</summary>
    private static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_$"u8);

    /// <summary>BigInt suffix — TS uses <c>n</c> on numeric literals to mark <c>BigInt</c>.</summary>
    private static readonly SearchValues<byte> BigintSuffix = SearchValues.Create("n"u8);

    /// <summary>First-byte set for backtick template literals.</summary>
    private static readonly SearchValues<byte> BacktickFirst = SearchValues.Create("`"u8);

    /// <summary>First-byte set for operator tokens.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("?=.<>!&|+-*/%^~"u8);

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the rule list. Exposed internal so <see cref="JavaScriptLexer"/> can reuse the patterns.</summary>
    /// <returns>Ordered rule list.</returns>
    internal static LexerRule[] BuildRules()
    {
        // Backtick template literal — interpolation expressions classify as part of the string body.
        var templateString = new LexerRule(
            static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'`'),
            TokenClass.StringDouble,
            LexerRule.NoStateChange) { FirstBytes = BacktickFirst };

        CFamilyConfig config = new()
        {
            Keywords = GeneralKeywords,
            KeywordTypes = TypeKeywords,
            KeywordDeclarations = DeclarationKeywords,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst,
            Punctuation = LanguageCommon.CCurlyPunctuationFirst,
            IntegerSuffix = BigintSuffix,
            FloatSuffix = BigintSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = templateString,
            IdentifierFirst = IdentifierFirst,
            IdentifierContinue = IdentifierContinue
        };

        return CFamilyRules.Build(config);
    }

    /// <summary>Builds the lexer.</summary>
    /// <returns>Configured lexer.</returns>
    private static Lexer Build() => new(LanguageRuleBuilder.BuildSingleState(BuildRules()));
}
