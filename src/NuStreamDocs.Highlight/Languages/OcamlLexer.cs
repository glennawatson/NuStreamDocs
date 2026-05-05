// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>OCaml lexer.</summary>
/// <remarks>
/// ML-family lexer — nested <c>(* … *)</c> block comments, no line comments,
/// <c>'a</c> type variables, and the standard OCaml keyword set.
/// </remarks>
public static class OcamlLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "and"u8],
        [.. "as"u8],
        [.. "begin"u8],
        [.. "do"u8],
        [.. "done"u8],
        [.. "downto"u8],
        [.. "else"u8],
        [.. "end"u8],
        [.. "for"u8],
        [.. "if"u8],
        [.. "in"u8],
        [.. "lazy"u8],
        [.. "match"u8],
        [.. "new"u8],
        [.. "of"u8],
        [.. "or"u8],
        [.. "rec"u8],
        [.. "then"u8],
        [.. "to"u8],
        [.. "try"u8],
        [.. "when"u8],
        [.. "while"u8],
        [.. "with"u8],
        [.. "raise"u8],
        [.. "assert"u8],
        [.. "function"u8]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "bool"u8],
        [.. "char"u8],
        [.. "float"u8],
        [.. "int"u8],
        [.. "list"u8],
        [.. "string"u8],
        [.. "unit"u8],
        [.. "array"u8],
        [.. "option"u8],
        [.. "ref"u8]);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "let"u8],
        [.. "fun"u8],
        [.. "type"u8],
        [.. "module"u8],
        [.. "open"u8],
        [.. "exception"u8],
        [.. "external"u8],
        [.. "include"u8],
        [.. "sig"u8],
        [.. "struct"u8],
        [.. "val"u8],
        [.. "method"u8],
        [.. "class"u8],
        [.. "object"u8],
        [.. "private"u8],
        [.. "mutable"u8],
        [.. "virtual"u8],
        [.. "constraint"u8],
        [.. "inherit"u8],
        [.. "initializer"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "None"u8],
        [.. "Some"u8]);

    /// <summary>Operator alternation.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "->"u8],
        [.. "<-"u8],
        [.. "|>"u8],
        [.. "<>"u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "::"u8],
        [.. ":="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "@"u8],
        [.. "^"u8],
        [.. "!"u8]
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abdefilmnoprtw"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abcfilrsu"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("celfimopstv"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfNS"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/=<>|&!:^@"u8);

    /// <summary>Gets the singleton OCaml lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the OCaml lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        MlFamilyConfig config = new()
        {
            BlockCommentOpen = [.. "(*"u8],
            BlockCommentClose = [.. "*)"u8],
            LineCommentPrefix = null,
            Keywords = Keywords,
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst
        };

        return MlFamilyRules.CreateLexer(config);
    }
}
