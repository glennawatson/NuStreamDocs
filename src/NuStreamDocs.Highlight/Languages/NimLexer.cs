// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Nim lexer.</summary>
/// <remarks>
/// <c>#</c> line comments, <c>#[ ... ]#</c> block comments, the
/// <c>proc</c>/<c>template</c>/<c>macro</c>/<c>type</c> declaration set, plus
/// the standard control-flow keywords. Indentation is consumed as plain
/// whitespace; structure-block detection isn't tracked at the lexer level.
/// </remarks>
public static class NimLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "elif"u8],
        [.. "else"u8],
        [.. "when"u8],
        [.. "case"u8],
        [.. "of"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "block"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "return"u8],
        [.. "yield"u8],
        [.. "raise"u8],
        [.. "try"u8],
        [.. "except"u8],
        [.. "finally"u8],
        [.. "discard"u8],
        [.. "import"u8],
        [.. "from"u8],
        [.. "include"u8],
        [.. "as"u8],
        [.. "in"u8],
        [.. "notin"u8],
        [.. "is"u8],
        [.. "isnot"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "xor"u8],
        [.. "div"u8],
        [.. "mod"u8],
        [.. "shl"u8],
        [.. "shr"u8],
        [.. "echo"u8]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "bool"u8],
        [.. "char"u8],
        [.. "string"u8],
        [.. "cstring"u8],
        [.. "int"u8],
        [.. "int8"u8],
        [.. "int16"u8],
        [.. "int32"u8],
        [.. "int64"u8],
        [.. "uint"u8],
        [.. "uint8"u8],
        [.. "uint16"u8],
        [.. "uint32"u8],
        [.. "uint64"u8],
        [.. "float"u8],
        [.. "float32"u8],
        [.. "float64"u8],
        [.. "byte"u8],
        [.. "void"u8],
        [.. "auto"u8],
        [.. "any"u8],
        [.. "seq"u8],
        [.. "array"u8],
        [.. "set"u8],
        [.. "tuple"u8],
        [.. "range"u8],
        [.. "openArray"u8]);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "proc"u8],
        [.. "func"u8],
        [.. "method"u8],
        [.. "iterator"u8],
        [.. "converter"u8],
        [.. "template"u8],
        [.. "macro"u8],
        [.. "type"u8],
        [.. "var"u8],
        [.. "let"u8],
        [.. "const"u8],
        [.. "object"u8],
        [.. "enum"u8],
        [.. "static"u8],
        [.. "ref"u8],
        [.. "ptr"u8],
        [.. "concept"u8],
        [.. "distinct"u8],
        [.. "export"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "..="u8],
        [.. "<<="u8],
        [.. ">>="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. ".."u8],
        [.. "->"u8],
        [.. "=>"u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
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

    /// <summary>First-byte set for the <c>#</c> comment dispatch.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefimnoryt sxywh"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abcdefiopstuvr"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("cdefilmoprstv"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Gets the singleton Nim lexer.</summary>
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

    /// <summary>Matches a Nim <c>#[ ... ]#</c> block comment.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPairedBlockComment(slice, "#["u8, "]#"u8);
}
