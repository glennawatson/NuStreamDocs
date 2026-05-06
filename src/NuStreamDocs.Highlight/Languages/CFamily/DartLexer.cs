// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Dart lexer.</summary>
/// <remarks>
/// Brace-style language without character literals (Dart has no <c>'x'</c> char form;
/// single quotes are strings) and without preprocessor directives. <c>${expr}</c>
/// interpolation is folded into the surrounding string token.
/// </remarks>
public static class DartLexer
{
    /// <summary>Gets the singleton Dart lexer.</summary>
    public static Lexer Instance { get; } = CFamilyRules.CreateBraceAnnotationLexer(
        new()
        {
            Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
                CFamilyShared.ControlFlowLiteral,
                "new this super is as in yield async await import library part show hide deferred rethrow assert"u8),
            KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
                "bool int double num String void dynamic Object Never Null Function Future Stream List Map Set Iterable"u8),
            KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
                "var final const late static abstract external factory covariant operator get set class interface mixin enum typedef extends implements with on sealed base"u8),
            KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral),
            Operators = OperatorAlternationFactory.SplitLongestFirst("??= ?? ?. ... .."u8, CFamilyShared.StandardOperatorsLiteral),
            OperatorFirst = CFamilyShared.StandardOperatorFirst
        },
        integerSuffix: CFamilyRules.NoSuffix,
        floatSuffix: CFamilyRules.NoSuffix,
        includeDocComment: true,
        includeCharacterLiteral: false,
        specialString: CFamilyRules.CreateTripleDoubleQuotedRawStringRule(minQuotes: 3));
}
