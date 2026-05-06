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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        MlFamilyShared.CommonKeywordsLiteral,
        "exposing"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Bool Char Float Int String List Maybe Result Cmd Sub Html Dict Set"u8);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "module import type alias port effect"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "True False Nothing Just Ok Err"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ":: -> <| |> <= >= /= == ++ .. && || + - * / = < > ."u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = OperatorAlternationFactory.FirstBytesOf(OperatorTable);

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
