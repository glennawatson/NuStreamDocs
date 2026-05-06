// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Functional;

/// <summary>Haskell lexer.</summary>
/// <remarks>
/// ML-family lexer — nested <c>{- … -}</c> block comments, <c>--</c> line comments,
/// <c>'a</c> type variables, and the standard Haskell keyword set.
/// </remarks>
public static class HaskellLexer
{
    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ":: -> <- => <> <= >= .. && || == /= ++ $$ $ + - * / = < > . @"u8);

    /// <summary>Gets the singleton Haskell lexer.</summary>
    public static Lexer Instance { get; } = MlFamilyRules.CreateLexer(new()
    {
        BlockCommentOpen = [.. "{-"u8],
        BlockCommentClose = [.. "-}"u8],
        LineCommentPrefix = [.. "--"u8],
        Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
            MlFamilyShared.CommonKeywordsLiteral,
            "do qualified hiding infix infixl infixr deriving default foreign"u8),
        KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
            "Bool Char Double Float Int Integer String Maybe Either IO Word Word8 Word16 Word32 Word64"u8),
        KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
            "module import data newtype type class instance family"u8),
        KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
            "True False Nothing Just Left Right"u8),
        Operators = OperatorTable
    });
}
