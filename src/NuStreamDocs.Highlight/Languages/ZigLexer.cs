// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Zig lexer.</summary>
/// <remarks>
/// Brace-style language with character literals and the multi-line raw-string form
/// (<c>\\</c> followed by line content) folded into the regular text token. Doc
/// comments use <c>///</c>.
/// </remarks>
public static class ZigLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus Zig-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "errdefer"u8],
        [.. "defer"u8],
        [.. "orelse"u8],
        [.. "unreachable"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "test"u8],
        [.. "comptime"u8],
        [.. "nosuspend"u8],
        [.. "suspend"u8],
        [.. "resume"u8],
        [.. "async"u8],
        [.. "await"u8],
        [.. "asm"u8],
        [.. "volatile"u8],
        [.. "linksection"u8],
        [.. "callconv"u8]]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "bool"u8],
        [.. "void"u8],
        [.. "noreturn"u8],
        [.. "type"u8],
        [.. "anyerror"u8],
        [.. "anytype"u8],
        [.. "anyopaque"u8],
        [.. "comptime_int"u8],
        [.. "comptime_float"u8],
        [.. "i8"u8],
        [.. "i16"u8],
        [.. "i32"u8],
        [.. "i64"u8],
        [.. "i128"u8],
        [.. "isize"u8],
        [.. "u8"u8],
        [.. "u16"u8],
        [.. "u32"u8],
        [.. "u64"u8],
        [.. "u128"u8],
        [.. "usize"u8],
        [.. "f16"u8],
        [.. "f32"u8],
        [.. "f64"u8],
        [.. "f80"u8],
        [.. "f128"u8],
        [.. "c_char"u8],
        [.. "c_short"u8],
        [.. "c_int"u8],
        [.. "c_long"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "const"u8],
        [.. "var"u8],
        [.. "fn"u8],
        [.. "struct"u8],
        [.. "enum"u8],
        [.. "union"u8],
        [.. "opaque"u8],
        [.. "error"u8],
        [.. "pub"u8],
        [.. "extern"u8],
        [.. "export"u8],
        [.. "inline"u8],
        [.. "noinline"u8],
        [.. "packed"u8],
        [.. "threadlocal"u8],
        [.. "usingnamespace"u8],
        [.. "allowzero"u8],
        [.. "align"u8]);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c> plus Zig's <c>undefined</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. CFamilyShared.TrueFalseNull, [.. "undefined"u8]]);

    /// <summary>Operator alternation — shared C-style core plus Zig's wrap-around / saturate forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "..."u8],
        [.. ".."u8],
        [.. "=>"u8],
        [.. "+%"u8],
        [.. "-%"u8],
        [.. "*%"u8],
        [.. "+|"u8],
        [.. "-|"u8],
        [.. "*|"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("aborcdefilnstuwv"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abcfintuv"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acefinopstuv"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfnu"u8);

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
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
