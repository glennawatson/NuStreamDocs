// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Scripting;

/// <summary>MATLAB / Octave lexer.</summary>
/// <remarks>
/// <c>%</c> line comments, <c>%{ ... %}</c> block comments (MATLAB only;
/// Octave shares the form), <c>function</c>/<c>end</c> blocks, and the
/// standard control-flow keyword set.
/// </remarks>
public static class MatlabLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "if elseif else end endif endfor endwhile endswitch endfunction for while do until switch case otherwise break continue return try catch throw"u8);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "function classdef properties methods events enumeration global persistent"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("true false"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "== ~= <= >= && || .* ./ .^ .' + - * / \\ ^ & | ~ = < > :"u8);

    /// <summary>First-byte set for the <c>%</c> comment dispatch.</summary>
    private static readonly SearchValues<byte> PercentFirst = SearchValues.Create("%"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/\\^&|~=<>:."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@"u8);

    /// <summary>Gets the singleton MATLAB / Octave lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        PreCommentRule = new(MatchBlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = PercentFirst },
        LineComment = new(static slice => TokenMatchers.MatchLineCommentToEol(slice, (byte)'%'), TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = PercentFirst },
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = KeywordConstants,
        KeywordDeclarations = KeywordDeclarations,
        Keywords = Keywords,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

    /// <summary>Matches a MATLAB <c>%{ ... %}</c> block comment.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPairedBlockComment(slice, "%{"u8, "%}"u8);
}
