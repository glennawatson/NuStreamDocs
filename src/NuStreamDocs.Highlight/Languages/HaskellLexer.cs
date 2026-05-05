// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Haskell lexer.</summary>
/// <remarks>
/// ML-family lexer — nested <c>{- … -}</c> block comments, <c>--</c> line comments,
/// <c>'a</c> type variables, and the standard Haskell keyword set.
/// </remarks>
public static class HaskellLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "then"u8],
        [.. "else"u8],
        [.. "case"u8],
        [.. "of"u8],
        [.. "do"u8],
        [.. "where"u8],
        [.. "let"u8],
        [.. "in"u8],
        [.. "as"u8],
        [.. "qualified"u8],
        [.. "hiding"u8],
        [.. "infix"u8],
        [.. "infixl"u8],
        [.. "infixr"u8],
        [.. "deriving"u8],
        [.. "default"u8],
        [.. "foreign"u8]);

    /// <summary>Built-in nominal type keywords (Prelude).</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Bool"u8],
        [.. "Char"u8],
        [.. "Double"u8],
        [.. "Float"u8],
        [.. "Int"u8],
        [.. "Integer"u8],
        [.. "String"u8],
        [.. "Maybe"u8],
        [.. "Either"u8],
        [.. "IO"u8],
        [.. "Word"u8],
        [.. "Word8"u8],
        [.. "Word16"u8],
        [.. "Word32"u8],
        [.. "Word64"u8]);

    /// <summary>Declaration / module keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "module"u8],
        [.. "import"u8],
        [.. "data"u8],
        [.. "newtype"u8],
        [.. "type"u8],
        [.. "class"u8],
        [.. "instance"u8],
        [.. "family"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "True"u8],
        [.. "False"u8],
        [.. "Nothing"u8],
        [.. "Just"u8],
        [.. "Left"u8],
        [.. "Right"u8]);

    /// <summary>Operator alternation.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "::"u8],
        [.. "->"u8],
        [.. "<-"u8],
        [.. "=>"u8],
        [.. "<>"u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. ".."u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "=="u8],
        [.. "/="u8],
        [.. "++"u8],
        [.. "$$"u8],
        [.. "$"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "."u8],
        [.. "@"u8]
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("acdefhilnoqtw"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("BCDEFIMSW"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("cdfimnt"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("TFNJLR"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/=<>|&!:.@$"u8);

    /// <summary>Gets the singleton Haskell lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Haskell lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        MlFamilyConfig config = new()
        {
            BlockCommentOpen = [.. "{-"u8],
            BlockCommentClose = [.. "-}"u8],
            LineCommentPrefix = [.. "--"u8],
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
