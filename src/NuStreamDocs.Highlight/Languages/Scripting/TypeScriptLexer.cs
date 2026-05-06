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
    private static readonly ByteKeywordSet DeclarationKeywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "var let const function class interface enum type namespace module export import from as declare abstract public private protected static async"u8);

    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet GeneralKeywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "if else for while do return switch case default break continue throw try catch finally new delete"u8,
        "in of instanceof typeof void yield await this super extends implements"u8);

    /// <summary>Built-in TS type-keyword set.</summary>
    private static readonly ByteKeywordSet TypeKeywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "any boolean number string void never unknown object symbol bigint readonly keyof"u8);

    /// <summary>Boolean / null / undefined / NaN / Infinity literal set.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false null undefined NaN Infinity"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ">>>= ??= ?. ... <<= >>= === !== &&= ||= **= <= >= == != && || ++ -- << >>> >> += -= *= /= %= &= |= ^= ** ?? => < > + - * / % & | ^ ! ~ = ?"u8);

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
