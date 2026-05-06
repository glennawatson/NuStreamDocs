// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Zig lexer.</summary>
/// <remarks>
/// Brace-style language with character literals and the multi-line raw-string form
/// (<c>\\</c> followed by line content) folded into the regular text token. Doc
/// comments use <c>///</c>.
/// </remarks>
public static class ZigLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus Zig-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "errdefer defer orelse unreachable and or test comptime nosuspend suspend resume async await asm volatile linksection callconv"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "bool void noreturn type anyerror anytype anyopaque comptime_int comptime_float"u8,
        "i8 i16 i32 i64 i128 isize u8 u16 u32 u64 u128 usize f16 f32 f64 f80 f128"u8,
        "c_char c_short c_int c_long"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "const var fn struct enum union opaque error pub extern export inline noinline packed threadlocal usingnamespace allowzero align"u8);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c> plus Zig's <c>undefined</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.TrueFalseNullLiteral,
        "undefined"u8);

    /// <summary>Operator alternation — shared C-style core plus Zig's wrap-around / saturate forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "... .. => +% -% *% +| -| *|"u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Single-byte structural punctuation — shared C-curly set plus the Zig <c>@</c> built-in marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Gets the singleton Zig lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Zig lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        return CFamilyRules.CreateLexer(config);
    }
}
