// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Scripting;

/// <summary>Julia lexer.</summary>
/// <remarks>
/// <c>#</c> line comments, <c>#=</c> ... <c>=#</c> block comments, the
/// <c>function</c>/<c>end</c>/<c>module</c>/<c>struct</c> declaration set,
/// and the standard control-flow keywords. <c>$expr</c> and
/// <c>$(expr)</c> string interpolation stay inside the surrounding string
/// token.
/// </remarks>
public static class JuliaLexer
{
    /// <summary>Gets the singleton Julia lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        PreCommentRule = LanguageCommon.CreatePairedBlockCommentRule([.. "#="u8], [.. "=#"u8], LanguageCommon.HashFirst),
        LineComment = LanguageCommon.CreateHashLineCommentRule(),
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("true false nothing missing"u8),
        KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
            "Bool Int Int8 Int16 Int32 Int64 Int128 UInt UInt8 UInt16 UInt32 UInt64 UInt128 Float16 Float32 Float64 Char String Symbol Any Nothing Missing Vector Matrix Array Tuple Dict Set"u8),
        KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
            "function macro module baremodule struct mutable abstract primitive type const"u8),
        Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
            "if elseif else end for while do begin let where in isa return break continue throw try catch finally import using export global local quote"u8),
        Operators = OperatorAlternationFactory.SplitLongestFirst(
            "<<= >>= ... <= >= == != && || << >> -> <: >: += -= *= /= %= // ^ + - * / % & | ! ~ = < > ?"u8),
        Punctuation = CFamilyShared.AnnotationColonPunctuation
    });
}
