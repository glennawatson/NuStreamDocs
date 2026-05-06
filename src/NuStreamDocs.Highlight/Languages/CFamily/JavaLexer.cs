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
    /// <summary>Minimum opening / closing quote run for a text-block literal.</summary>
    private const int TextBlockMinQuotes = 3;

    /// <summary>General-keyword set — shared C-family control-flow plus Java-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "throws new this super instanceof import package synchronized yield assert"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "boolean byte char short int long float double void var"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "class interface enum record extends implements permits sealed abstract final static public private protected transient volatile native strictfp"u8);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral);

    /// <summary>Operator alternation — shared C-style core plus Java's unsigned right-shift forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ">>>= >>> ::"u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Gets the singleton Java lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Java lexer.</summary>
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
        specialString: CFamilyRules.CreateTripleDoubleQuotedRawStringRule(TextBlockMinQuotes));
}
