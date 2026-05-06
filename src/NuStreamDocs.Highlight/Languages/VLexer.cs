// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>V lexer.</summary>
/// <remarks>
/// Brace-style language modeled on Go's surface — <c>fn</c>, <c>struct</c>,
/// <c>module</c>, <c>pub</c>, <c>mut</c>, with character literals and no
/// preprocessor. <c>$</c>-interpolation in strings stays inside the string
/// token.
/// </remarks>
public static class VLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus V-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "as"u8],
        [.. "in"u8],
        [.. "is"u8],
        [.. "or"u8],
        [.. "match"u8],
        [.. "select"u8],
        [.. "go"u8],
        [.. "goto"u8],
        [.. "defer"u8],
        [.. "spawn"u8],
        [.. "lock"u8],
        [.. "rlock"u8],
        [.. "unsafe"u8],
        [.. "asm"u8],
        [.. "shared"u8],
        [.. "atomic"u8],
        [.. "$if"u8],
        [.. "$else"u8],
        [.. "$for"u8]]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "bool"u8],
        [.. "byte"u8],
        [.. "i8"u8],
        [.. "i16"u8],
        [.. "int"u8],
        [.. "i64"u8],
        [.. "u8"u8],
        [.. "u16"u8],
        [.. "u32"u8],
        [.. "u64"u8],
        [.. "f32"u8],
        [.. "f64"u8],
        [.. "rune"u8],
        [.. "string"u8],
        [.. "voidptr"u8],
        [.. "size_t"u8],
        [.. "char"u8],
        [.. "any"u8],
        [.. "none"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "fn"u8],
        [.. "struct"u8],
        [.. "interface"u8],
        [.. "enum"u8],
        [.. "type"u8],
        [.. "module"u8],
        [.. "import"u8],
        [.. "pub"u8],
        [.. "mut"u8],
        [.. "const"u8],
        [.. "static"u8],
        [.. "__global"u8]);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(CFamilyShared.TrueFalseNull);

    /// <summary>Operator alternation — shared C-style core plus V's range / arrow forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "..."u8],
        [.. ".."u8],
        [.. ":="u8],
        [.. "<-"u8],
        [.. "->"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>Single-byte structural punctuation — shared C-curly set plus V's <c>@</c> attribute marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Gets the singleton V lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the V lexer.</summary>
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
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        return CFamilyRules.CreateLexer(config);
    }
}
