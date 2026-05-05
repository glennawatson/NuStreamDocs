// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Julia lexer.</summary>
/// <remarks>
/// <c>#</c> line comments, <c>#=</c> ... <c>=#</c> block comments, the
/// <c>function</c>/<c>end</c>/<c>module</c>/<c>struct</c> declaration set,
/// and the standard control-flow keywords. <c>$expr</c> and
/// <c>$(expr)</c> string interpolation stay inside the surrounding string
/// token.
/// </remarks>
public static class JuliaLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "elseif"u8],
        [.. "else"u8],
        [.. "end"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "begin"u8],
        [.. "let"u8],
        [.. "where"u8],
        [.. "in"u8],
        [.. "isa"u8],
        [.. "return"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "import"u8],
        [.. "using"u8],
        [.. "export"u8],
        [.. "global"u8],
        [.. "local"u8],
        [.. "quote"u8]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Bool"u8],
        [.. "Int"u8],
        [.. "Int8"u8],
        [.. "Int16"u8],
        [.. "Int32"u8],
        [.. "Int64"u8],
        [.. "Int128"u8],
        [.. "UInt"u8],
        [.. "UInt8"u8],
        [.. "UInt16"u8],
        [.. "UInt32"u8],
        [.. "UInt64"u8],
        [.. "UInt128"u8],
        [.. "Float16"u8],
        [.. "Float32"u8],
        [.. "Float64"u8],
        [.. "Char"u8],
        [.. "String"u8],
        [.. "Symbol"u8],
        [.. "Any"u8],
        [.. "Nothing"u8],
        [.. "Missing"u8],
        [.. "Vector"u8],
        [.. "Matrix"u8],
        [.. "Array"u8],
        [.. "Tuple"u8],
        [.. "Dict"u8],
        [.. "Set"u8]);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "function"u8],
        [.. "macro"u8],
        [.. "module"u8],
        [.. "baremodule"u8],
        [.. "struct"u8],
        [.. "mutable"u8],
        [.. "abstract"u8],
        [.. "primitive"u8],
        [.. "type"u8],
        [.. "const"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nothing"u8],
        [.. "missing"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<<="u8],
        [.. ">>="u8],
        [.. "..."u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "<<"u8],
        [.. ">>"u8],
        [.. "->"u8],
        [.. "<:"u8],
        [.. ">:"u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
        [.. "//"u8],
        [.. "^"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "&"u8],
        [.. "|"u8],
        [.. "!"u8],
        [.. "~"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "?"u8]
    ];

    /// <summary>First-byte set for the <c>#</c> comment dispatch.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("bcdefgilqrtuw"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("ABCDFIMSTNUV"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("abcfmpst"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfnm"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Gets the singleton Julia lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        PreCommentRule = new(MatchBlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = HashFirst },
        LineComment = new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = KeywordConstants,
        KeywordConstantFirst = KeywordConstantFirst,
        KeywordTypes = KeywordTypes,
        KeywordTypeFirst = KeywordTypeFirst,
        KeywordDeclarations = KeywordDeclarations,
        KeywordDeclarationFirst = KeywordDeclarationFirst,
        Keywords = Keywords,
        KeywordFirst = KeywordFirst,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

    /// <summary>Matches a Julia <c>#=</c> ... <c>=#</c> block comment.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPairedBlockComment(slice, "#="u8, "=#"u8);
}
