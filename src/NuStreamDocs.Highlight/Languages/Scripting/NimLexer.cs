// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Scripting;

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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "if elif else when case of for while do block break continue return yield raise try except finally discard import from include as in notin is isnot and or not xor div mod shl shr echo"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "bool char string cstring int int8 int16 int32 int64 uint uint8 uint16 uint32 uint64 float float32 float64 byte void auto any seq array set tuple range openArray"u8);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "proc func method iterator converter template macro type var let const object enum static ref ptr concept distinct export"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNilLiteral);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "..= <<= >>= && || .. -> => <= >= == != += -= *= /= %= + - * / % & | ^ ! ~ = < > ?"u8);

    /// <summary>First-byte set for the <c>#</c> comment dispatch.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

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
        KeywordTypes = KeywordTypes,
        KeywordDeclarations = KeywordDeclarations,
        Keywords = Keywords,
        Operators = OperatorTable,
        Punctuation = CFamilyShared.AnnotationColonPunctuation
    });

    /// <summary>Matches a Nim <c>#[ ... ]#</c> block comment.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPairedBlockComment(slice, "#["u8, "]#"u8);
}
