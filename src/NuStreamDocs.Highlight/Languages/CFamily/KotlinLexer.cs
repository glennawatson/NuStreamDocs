// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Kotlin lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the
/// triple-quoted raw-string form folded into the special-string rule.
/// </remarks>
public static class KotlinLexer
{
    /// <summary>Gets the singleton Kotlin lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateBraceAnnotationLexer(
        new()
        {
            Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
                CFamilyShared.ControlFlowLiteral,
                "when is as in out by where yield this super import package"u8),
            KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
                "Boolean Byte Char Short Int Long Float Double String Unit Any Nothing Array List Map Set"u8),
            KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
                "val var fun class interface object enum data sealed open abstract final override lateinit const inline infix tailrec operator suspend vararg noinline crossinline reified"u8,
                "internal public private protected companion annotation typealias"u8),
            KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral),
            Operators = OperatorAlternationFactory.SplitLongestFirst(
                "?: ..= .. -> :: == != <= >= && || ++ -- += -= *= /= %= ?. !! + - * / % ! = < > ?"u8),
            OperatorFirst = CFamilyShared.StandardOperatorFirst
        },
        integerSuffix: SearchValues.Create("Llu"u8),
        floatSuffix: SearchValues.Create("fF"u8),
        includeDocComment: false,
        includeCharacterLiteral: true,
        specialString: CFamilyRules.CreateTripleDoubleQuotedRawStringRule(minQuotes: 3));
}
