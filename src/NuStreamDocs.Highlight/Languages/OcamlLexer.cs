// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "and as begin do done downto else end for if in lazy match new of or rec then to try when while with raise assert function"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "bool char float int list string unit array option ref"u8);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "let fun type module open exception external include sig struct val method class object private mutable virtual constraint inherit initializer"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false None Some"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "-> <- |> <> <= >= :: := && || == != + - * / = < > @ ^ !"u8);

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
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable
        };

        return MlFamilyRules.CreateLexer(config);
    }
}
