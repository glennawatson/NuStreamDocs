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
    /// <summary>Minimum opening / closing quote run for a triple-quoted raw string.</summary>
    private const int RawStringMinQuotes = 3;

    /// <summary>General-keyword set — shared C-family control-flow plus Scala-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "match yield new this super import package with extends derives as given using then"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Boolean Byte Short Int Long Float Double Char String Unit Any AnyVal AnyRef Nothing Null Option List Seq Map Set Array"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "val var def type class object trait enum abstract final sealed open implicit lazy override private protected public inline transparent opaque extension"u8);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral);

    /// <summary>Operator alternation — shared C-style core plus Scala's <c>&lt;-</c> / <c>=&gt;</c> / <c>::</c> arrows, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "<- => ::"u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Gets the singleton Scala lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Scala lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build() => CFamilyRules.CreateBraceAnnotationLexer(
        new()
        {
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst
        },
        integerSuffix: CFamilyShared.JvmIntegerSuffix,
        floatSuffix: CFamilyShared.JvmFloatSuffix,
        includeDocComment: false,
        includeCharacterLiteral: true,
        specialString: CFamilyRules.CreateTripleDoubleQuotedRawStringRule(RawStringMinQuotes));
}
