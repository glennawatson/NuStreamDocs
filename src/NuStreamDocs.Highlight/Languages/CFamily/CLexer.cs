// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>C lexer.</summary>
/// <remarks>
/// Brace-style language with <c>#</c>-preprocessor directives and character
/// literals; no doc comments and no raw strings.
/// </remarks>
public static class CLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus C-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        CFamilyShared.CExtraKeywordsLiteral,
        "_Alignof _Alignas _Atomic _Generic _Noreturn _Static_assert _Thread_local"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.CPrimitiveTypesLiteral,
        CFamilyShared.CSizedIntegerTypesLiteral,
        "_Bool _Complex _Imaginary FILE"u8);

    /// <summary>Declaration / storage-class / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "auto register static extern const volatile inline restrict struct union enum"u8);

    /// <summary>Constant keywords — <c>true</c> / <c>false</c> via the shared set, plus the C-specific <c>NULL</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false NULL"u8);

    /// <summary>Gets the singleton C lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the C lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        CFamilyConfig config = new()
        {
            Tables = new()
            {
                Keywords = Keywords,
                KeywordTypes = KeywordTypes,
                KeywordDeclarations = KeywordDeclarations,
                KeywordConstants = KeywordConstants,
                Operators = CFamilyShared.StandardOperators,
                OperatorFirst = CFamilyShared.StandardOperatorFirst
            },
            Punctuation = CFamilyShared.StandardPunctuation,
            IntegerSuffix = CFamilyShared.CIntegerSuffix,
            FloatSuffix = CFamilyShared.CFloatSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = true,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        return CFamilyRules.CreateLexer(config);
    }
}
