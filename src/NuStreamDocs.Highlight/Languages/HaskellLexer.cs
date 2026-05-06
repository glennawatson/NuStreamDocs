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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        MlFamilyShared.CommonKeywordsLiteral,
        "do qualified hiding infix infixl infixr deriving default foreign"u8);

    /// <summary>Built-in nominal type keywords (Prelude).</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Bool Char Double Float Int Integer String Maybe Either IO Word Word8 Word16 Word32 Word64"u8);

    /// <summary>Declaration / module keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "module import data newtype type class instance family"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "True False Nothing Just Left Right"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ":: -> <- => <> <= >= .. && || == /= ++ $$ $ + - * / = < > . @"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = OperatorAlternationFactory.FirstBytesOf(OperatorTable);

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
