// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// <summary>Gets the singleton Nim lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        PreCommentRule = LanguageCommon.CreatePairedBlockCommentRule([.. "#["u8], [.. "]#"u8], LanguageCommon.HashFirst),
        LineComment = LanguageCommon.CreateHashLineCommentRule(),
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNilLiteral),
        KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
            "bool char string cstring int int8 int16 int32 int64 uint uint8 uint16 uint32 uint64 float float32 float64 byte void auto any seq array set tuple range openArray"u8),
        KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
            "proc func method iterator converter template macro type var let const object enum static ref ptr concept distinct export"u8),
        Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
            "if elif else when case of for while do block break continue return yield raise try except finally discard import from include as in notin is isnot and or not xor div mod shl shr echo"u8),
        Operators = OperatorAlternationFactory.SplitLongestFirst(
            "..= <<= >>= && || .. -> => <= >= == != += -= *= /= %= + - * / % & | ^ ! ~ = < > ?"u8),
        Punctuation = CFamilyShared.AnnotationColonPunctuation
    });
}
