// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Swift lexer.</summary>
/// <remarks>
/// Brace-style language with character literals folded into the string rule
/// (Swift uses <c>"x"</c>, not <c>'x'</c>), and the multi-line raw string
/// <c>"""..."""</c> form folded into the special-string rule.
/// </remarks>
public static class SwiftLexer
{
    /// <summary>Minimum opening / closing quote run for a multi-line string literal.</summary>
    private const int MultiLineStringMinQuotes = 3;

    /// <summary>General-keyword set — shared C-family control-flow plus Swift-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "guard repeat fallthrough throws rethrows defer where as is in self Self super import async await actor some any inout init deinit subscript willSet didSet get set"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Bool Character String Int Int8 Int16 Int32 Int64 UInt UInt8 UInt16 UInt32 UInt64 Float Double Void Any AnyObject Optional Array Dictionary Set Result"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "let var func class struct enum protocol extension typealias associatedtype static public private internal fileprivate open final lazy weak unowned"u8,
        "mutating nonmutating override required convenience dynamic indirect operator precedencegroup"u8);

    /// <summary>Constant keywords — Swift uses <c>nil</c> in place of the shared <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNilLiteral);

    /// <summary>Operator alternation — shared C-style core plus Swift's range / nil-coalesce / optional-chain forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "... ..< ?? ?."u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Gets the singleton Swift lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Swift lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        // Multi-line strings come first; the regular double-string rule below
        // handles single-line strings (including \( ... ) interpolation, which
        // is folded into the string body — themes still colour the literal,
        // and the inner expression isn't re-entered without a state stack).
        var multiLineString = new LexerRule(
            static slice => TokenMatchers.MatchRawQuotedString(slice, (byte)'"', MultiLineStringMinQuotes),
            TokenClass.StringDouble,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst };

        CFamilyConfig config = new()
        {
            Tables = new()
            {
                Keywords = Keywords,
                KeywordTypes = KeywordTypes,
                KeywordDeclarations = KeywordDeclarations,
                KeywordConstants = KeywordConstants,
                Operators = OperatorTable,
                OperatorFirst = CFamilyShared.StandardOperatorFirst
            },
            Punctuation = CFamilyShared.AnnotationColonPunctuation,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = multiLineString
        };

        return CFamilyRules.CreateLexer(config);
    }
}
