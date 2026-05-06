// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

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
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "if elseif else end for while do begin let where in isa return break continue throw try catch finally import using export global local quote"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Bool Int Int8 Int16 Int32 Int64 Int128 UInt UInt8 UInt16 UInt32 UInt64 UInt128 Float16 Float32 Float64 Char String Symbol Any Nothing Missing Vector Matrix Array Tuple Dict Set"u8);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "function macro module baremodule struct mutable abstract primitive type const"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("true false nothing missing"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "<<= >>= ... <= >= == != && || << >> -> <: >: += -= *= /= %= // ^ + - * / % & | ! ~ = < > ?"u8);

    /// <summary>First-byte set for the <c>#</c> comment dispatch.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = OperatorAlternationFactory.FirstBytesOf(OperatorTable);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Gets the singleton Julia lexer.</summary>
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
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

    /// <summary>Matches a Julia <c>#=</c> ... <c>=#</c> block comment.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPairedBlockComment(slice, "#="u8, "=#"u8);
}
