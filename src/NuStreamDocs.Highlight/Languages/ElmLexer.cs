// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Elm lexer.</summary>
/// <remarks>
/// ML-family lexer — same comment shape as Haskell (<c>{- ... -}</c> nested block,
/// <c>--</c> line) with Elm's keyword set.
/// </remarks>
public static class ElmLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. MlFamilyShared.CommonKeywords,
        [.. "exposing"u8]]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Bool"u8],
        [.. "Char"u8],
        [.. "Float"u8],
        [.. "Int"u8],
        [.. "String"u8],
        [.. "List"u8],
        [.. "Maybe"u8],
        [.. "Result"u8],
        [.. "Cmd"u8],
        [.. "Sub"u8],
        [.. "Html"u8],
        [.. "Dict"u8],
        [.. "Set"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "module"u8],
        [.. "import"u8],
        [.. "type"u8],
        [.. "alias"u8],
        [.. "port"u8],
        [.. "effect"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "True"u8],
        [.. "False"u8],
        [.. "Nothing"u8],
        [.. "Just"u8],
        [.. "Ok"u8],
        [.. "Err"u8]);

    /// <summary>Operator alternation.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "::"u8],
        [.. "->"u8],
        [.. "<|"u8],
        [.. "|>"u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "/="u8],
        [.. "=="u8],
        [.. "++"u8],
        [.. ".."u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "."u8]
    ];

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/=<>|&!:."u8);

    /// <summary>Gets the singleton Elm lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Elm lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        MlFamilyConfig config = new()
        {
            BlockCommentOpen = [.. "{-"u8],
            BlockCommentClose = [.. "-}"u8],
            LineCommentPrefix = [.. "--"u8],
            Keywords = Keywords,
            KeywordFirst = Keywords.FirstByteSet,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypes.FirstByteSet,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarations.FirstByteSet,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstants.FirstByteSet,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst
        };

        return MlFamilyRules.CreateLexer(config);
    }
}
