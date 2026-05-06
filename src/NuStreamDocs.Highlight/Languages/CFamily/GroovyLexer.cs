// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Groovy lexer (covers Gradle build files via the <c>gradle</c> alias).</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and triple-quoted raw
/// strings (matching Java's text-block shape) folded into the special-string rule.
/// </remarks>
public static class GroovyLexer
{
    /// <summary>Gets the singleton Groovy lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateBraceAnnotationLexer(
        new()
        {
            Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
                CFamilyShared.ControlFlowLiteral,
                "throws new this super instanceof import package as in assert"u8),
            KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
                "boolean byte char short int long float double void"u8),
            KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
                "def class interface trait enum extends implements abstract final static public private protected synchronized transient volatile"u8),
            KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral),
            Operators = OperatorAlternationFactory.SplitLongestFirst(">>>= >>> :: ?: ?. *."u8, CFamilyShared.StandardOperatorsLiteral),
            OperatorFirst = CFamilyShared.StandardOperatorFirst
        },
        integerSuffix: SearchValues.Create("lLgG"u8),
        floatSuffix: SearchValues.Create("fFdDgG"u8),
        includeDocComment: false,
        includeCharacterLiteral: true,
        specialString: CFamilyRules.CreateTripleDoubleQuotedRawStringRule(minQuotes: 3));
}
