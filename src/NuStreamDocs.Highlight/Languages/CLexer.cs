// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>C lexer.</summary>
/// <remarks>
/// Brace-style language with <c>#</c>-preprocessor directives and character
/// literals; no doc comments and no raw strings.
/// </remarks>
public static class CLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "else"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "switch"u8],
        [.. "case"u8],
        [.. "default"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "return"u8],
        [.. "goto"u8],
        [.. "sizeof"u8],
        [.. "typedef"u8],
        [.. "_Alignof"u8],
        [.. "_Alignas"u8],
        [.. "_Atomic"u8],
        [.. "_Generic"u8],
        [.. "_Noreturn"u8],
        [.. "_Static_assert"u8],
        [.. "_Thread_local"u8]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "char"u8],
        [.. "short"u8],
        [.. "int"u8],
        [.. "long"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "void"u8],
        [.. "signed"u8],
        [.. "unsigned"u8],
        [.. "_Bool"u8],
        [.. "_Complex"u8],
        [.. "_Imaginary"u8],
        [.. "size_t"u8],
        [.. "ssize_t"u8],
        [.. "ptrdiff_t"u8],
        [.. "int8_t"u8],
        [.. "int16_t"u8],
        [.. "int32_t"u8],
        [.. "int64_t"u8],
        [.. "uint8_t"u8],
        [.. "uint16_t"u8],
        [.. "uint32_t"u8],
        [.. "uint64_t"u8],
        [.. "FILE"u8]);

    /// <summary>Declaration / storage-class / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "auto"u8],
        [.. "register"u8],
        [.. "static"u8],
        [.. "extern"u8],
        [.. "const"u8],
        [.. "volatile"u8],
        [.. "inline"u8],
        [.. "restrict"u8],
        [.. "struct"u8],
        [.. "union"u8],
        [.. "enum"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "NULL"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<<="u8],
        [.. ">>="u8],
        [.. "->"u8],
        [.. "++"u8],
        [.. "--"u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "<<"u8],
        [.. ">>"u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
        [.. "&="u8],
        [.. "|="u8],
        [.. "^="u8],
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
        [.. "<"u8],
        [.. ">"u8],
        [.. "?"u8]
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("_bcdefgirstvw"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("_Fcdfilpsuv"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acersuvi"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfN"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:"u8);

    /// <summary>Integer / hex literal suffix bytes.</summary>
    private static readonly SearchValues<byte> IntegerSuffixSet = SearchValues.Create("uUlL"u8);

    /// <summary>Float-literal suffix bytes.</summary>
    private static readonly SearchValues<byte> FloatSuffixSet = SearchValues.Create("fFlL"u8);

    /// <summary>Gets the singleton C lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the C lexer.</summary>
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
            OperatorFirst = OperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = IntegerSuffixSet,
            FloatSuffix = FloatSuffixSet,
            IncludeDocComment = false,
            IncludePreprocessor = true,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
