// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Java lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the
/// Java 15 text-block <c>"""..."""</c> form folded into the special-string rule.
/// </remarks>
public static class JavaLexer
{
    /// <summary>Gets the singleton Java lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateBraceAnnotationLexer(
        new()
        {
            Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
                CFamilyShared.ControlFlowLiteral,
                "throws new this super instanceof import package synchronized yield assert"u8),
            KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
                "boolean byte char short int long float double void var"u8),
            KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
                "class interface enum record extends implements permits sealed abstract final static public private protected transient volatile native strictfp"u8),
            KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral),
            Operators = OperatorAlternationFactory.SplitLongestFirst(">>>= >>> ::"u8, CFamilyShared.StandardOperatorsLiteral),
            OperatorFirst = CFamilyShared.StandardOperatorFirst
        },
        integerSuffix: CFamilyShared.JvmIntegerSuffix,
        floatSuffix: CFamilyShared.JvmFloatSuffix,
        includeDocComment: false,
        includeCharacterLiteral: true,
        specialString: CFamilyRules.CreateTripleDoubleQuotedRawStringRule(minQuotes: 3));
}
