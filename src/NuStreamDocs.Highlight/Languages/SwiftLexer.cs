// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "guard"u8],
        [.. "repeat"u8],
        [.. "fallthrough"u8],
        [.. "throws"u8],
        [.. "rethrows"u8],
        [.. "defer"u8],
        [.. "where"u8],
        [.. "as"u8],
        [.. "is"u8],
        [.. "in"u8],
        [.. "self"u8],
        [.. "Self"u8],
        [.. "super"u8],
        [.. "import"u8],
        [.. "async"u8],
        [.. "await"u8],
        [.. "actor"u8],
        [.. "some"u8],
        [.. "any"u8],
        [.. "inout"u8],
        [.. "init"u8],
        [.. "deinit"u8],
        [.. "subscript"u8],
        [.. "willSet"u8],
        [.. "didSet"u8],
        [.. "get"u8],
        [.. "set"u8]]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Bool"u8],
        [.. "Character"u8],
        [.. "String"u8],
        [.. "Int"u8],
        [.. "Int8"u8],
        [.. "Int16"u8],
        [.. "Int32"u8],
        [.. "Int64"u8],
        [.. "UInt"u8],
        [.. "UInt8"u8],
        [.. "UInt16"u8],
        [.. "UInt32"u8],
        [.. "UInt64"u8],
        [.. "Float"u8],
        [.. "Double"u8],
        [.. "Void"u8],
        [.. "Any"u8],
        [.. "AnyObject"u8],
        [.. "Optional"u8],
        [.. "Array"u8],
        [.. "Dictionary"u8],
        [.. "Set"u8],
        [.. "Result"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "let"u8],
        [.. "var"u8],
        [.. "func"u8],
        [.. "class"u8],
        [.. "struct"u8],
        [.. "enum"u8],
        [.. "protocol"u8],
        [.. "extension"u8],
        [.. "typealias"u8],
        [.. "associatedtype"u8],
        [.. "static"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "internal"u8],
        [.. "fileprivate"u8],
        [.. "open"u8],
        [.. "final"u8],
        [.. "lazy"u8],
        [.. "weak"u8],
        [.. "unowned"u8],
        [.. "mutating"u8],
        [.. "nonmutating"u8],
        [.. "override"u8],
        [.. "required"u8],
        [.. "convenience"u8],
        [.. "dynamic"u8],
        [.. "indirect"u8],
        [.. "operator"u8],
        [.. "precedencegroup"u8]);

    /// <summary>Constant keywords — Swift uses <c>nil</c> in place of the shared <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8]);

    /// <summary>Operator alternation — shared C-style core plus Swift's range / nil-coalesce / optional-chain forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "..."u8],
        [.. "..<"u8],
        [.. "??"u8],
        [.. "?."u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("aSbcdefgiprstwx"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("ABCDFISURVO"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acdefilmnopqrstvuw"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>Single-byte structural punctuation — shared C-curly set plus the Swift <c>@</c> attribute marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

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
            Keywords = Keywords,
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = multiLineString
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
