// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Go lexer.</summary>
/// <remarks>
/// Brace-style language with <c>//</c> and <c>/* */</c> comments and the
/// backtick-delimited raw-string form folded into the special-string rule.
/// </remarks>
public static class GoLexer
{
    /// <summary>General-keyword set — shared C-family control-flow (Go has no try/catch/throw/finally; those entries simply don't fire) plus Go-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "defer"u8],
        [.. "fallthrough"u8],
        [.. "go"u8],
        [.. "goto"u8],
        [.. "range"u8],
        [.. "select"u8]]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "bool"u8],
        [.. "byte"u8],
        [.. "complex64"u8],
        [.. "complex128"u8],
        [.. "error"u8],
        [.. "float32"u8],
        [.. "float64"u8],
        [.. "int"u8],
        [.. "int8"u8],
        [.. "int16"u8],
        [.. "int32"u8],
        [.. "int64"u8],
        [.. "rune"u8],
        [.. "string"u8],
        [.. "uint"u8],
        [.. "uint8"u8],
        [.. "uint16"u8],
        [.. "uint32"u8],
        [.. "uint64"u8],
        [.. "uintptr"u8],
        [.. "any"u8],
        [.. "comparable"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "const"u8],
        [.. "func"u8],
        [.. "import"u8],
        [.. "interface"u8],
        [.. "map"u8],
        [.. "package"u8],
        [.. "struct"u8],
        [.. "type"u8],
        [.. "var"u8],
        [.. "chan"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8],
        [.. "iota"u8]);

    /// <summary>Operator alternation — shared C-style core plus Go's bit-clear / channel-receive / short-decl / spread forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "&^="u8],
        [.. ":="u8],
        [.. "<-"u8],
        [.. "..."u8],
        [.. "&^"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("bcdefgirs"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abceifrsu"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("cfimpstv"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfni"u8);

    /// <summary>Single-byte structural punctuation — Go has no <c>:</c> punctuation since <c>:</c> is part of <c>:=</c>.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,."u8);

    /// <summary>Numeric-literal suffix bytes (Go uses <c>i</c> for the imaginary suffix).</summary>
    private static readonly SearchValues<byte> NumericSuffixSet = SearchValues.Create("i"u8);

    /// <summary>First-byte set for the special-string rule (backtick raw string).</summary>
    private static readonly SearchValues<byte> BacktickFirst = SearchValues.Create("`"u8);

    /// <summary>Gets the singleton Go lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Go lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        var backtickRaw = new LexerRule(
            static slice => TokenMatchers.MatchBracketedBlock(slice, (byte)'`', (byte)'`'),
            TokenClass.StringDouble,
            LexerRule.NoStateChange) { FirstBytes = BacktickFirst };

        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = NumericSuffixSet,
            FloatSuffix = NumericSuffixSet,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = backtickRaw
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
