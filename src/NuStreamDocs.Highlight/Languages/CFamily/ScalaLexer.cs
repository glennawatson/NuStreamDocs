// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Scala lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the triple-quoted
/// raw-string form folded into the special-string rule. Covers Scala 3 / 2 keywords.
/// </remarks>
public static class ScalaLexer
{
    /// <summary>Gets the singleton Scala lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateBraceAnnotationLexer(
        new()
        {
            Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
                CFamilyShared.ControlFlowLiteral,
                "match yield new this super import package with extends derives as given using then"u8),
            KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
                "Boolean Byte Short Int Long Float Double Char String Unit Any AnyVal AnyRef Nothing Null Option List Seq Map Set Array"u8),
            KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
                "val var def type class object trait enum abstract final sealed open implicit lazy override private protected public inline transparent opaque extension"u8),
            KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral),
            Operators = OperatorAlternationFactory.SplitLongestFirst("<- => ::"u8, CFamilyShared.StandardOperatorsLiteral),
            OperatorFirst = CFamilyShared.StandardOperatorFirst
        },
        integerSuffix: CFamilyShared.JvmIntegerSuffix,
        floatSuffix: CFamilyShared.JvmFloatSuffix,
        includeDocComment: false,
        includeCharacterLiteral: true,
        specialString: CFamilyRules.CreateTripleDoubleQuotedRawStringRule(minQuotes: 3));
}
