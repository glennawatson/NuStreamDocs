// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Go lexer.</summary>
/// <remarks>
/// Brace-style language with <c>//</c> and <c>/* */</c> comments and the
/// backtick-delimited raw-string form folded into the special-string rule.
/// </remarks>
public static class GoLexer
{
    /// <summary>General-keyword set — shared C-family control-flow (Go has no try/catch/throw/finally; those entries simply don't fire) plus Go-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "defer fallthrough go goto range select"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "bool byte complex64 complex128 error float32 float64 int int8 int16 int32 int64 rune string uint uint8 uint16 uint32 uint64 uintptr any comparable"u8);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "const func import interface map package struct type var chan"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false nil iota"u8);

    /// <summary>Operator alternation — shared C-style core plus Go's bit-clear / channel-receive / short-decl / spread forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "&^= := <- ... &^"u8,
        CFamilyShared.StandardOperatorsLiteral);

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
            Tables = new()
            {
                Keywords = Keywords,
                KeywordTypes = KeywordTypes,
                KeywordDeclarations = KeywordDeclarations,
                KeywordConstants = KeywordConstants,
                Operators = OperatorTable,
                OperatorFirst = CFamilyShared.StandardOperatorFirst
            },
            Punctuation = PunctuationSet,
            IntegerSuffix = NumericSuffixSet,
            FloatSuffix = NumericSuffixSet,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = backtickRaw
        };

        return CFamilyRules.CreateLexer(config);
    }
}
