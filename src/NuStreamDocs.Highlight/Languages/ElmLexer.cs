// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Elm lexer.</summary>
/// <remarks>
/// ML-family lexer — same comment shape as Haskell (<c>{- ... -}</c> nested block,
/// <c>--</c> line) with Elm's keyword set.
/// </remarks>
public static class ElmLexer
{
    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ":: -> <| |> <= >= /= == ++ .. && || + - * / = < > ."u8);

    /// <summary>Gets the singleton Elm lexer.</summary>
    public static Lexer Instance { get; } = MlFamilyRules.CreateLexer(new()
    {
        BlockCommentOpen = [.. "{-"u8],
        BlockCommentClose = [.. "-}"u8],
        LineCommentPrefix = [.. "--"u8],
        Keywords = ByteKeywordSet.CreateFromSpaceSeparated(MlFamilyShared.CommonKeywordsLiteral, "exposing"u8),
        KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated("Bool Char Float Int String List Maybe Result Cmd Sub Html Dict Set"u8),
        KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated("module import type alias port effect"u8),
        KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("True False Nothing Just Ok Err"u8),
        Operators = OperatorTable
    });
}
