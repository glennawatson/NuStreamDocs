// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Objective-C lexer.</summary>
/// <remarks>
/// Brace-style language with C preprocessor and character literals; the Objective-C
/// <c>@interface</c> / <c>@implementation</c> / <c>@property</c> directives classify as
/// declaration keywords via the at-prefixed keyword table.
/// </remarks>
public static class ObjectiveCLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus the ObjC additions.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        CFamilyShared.CExtraKeywordsLiteral,
        "self super in out inout bycopy byref oneway nil"u8);

    /// <summary>Built-in primitive type keywords (C primitives plus ObjC <c>id</c> / <c>BOOL</c> / <c>SEL</c>).</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.CPrimitiveTypesLiteral,
        "id BOOL SEL Class IMP instancetype Protocol NSInteger NSUInteger NSString NSArray NSDictionary"u8);

    /// <summary>Declaration / qualifier keywords (C-style; the ObjC <c>@interface</c>-style directives are handled separately).</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "auto register static extern const volatile inline restrict struct union enum"u8);

    /// <summary>ObjC <c>@</c>-prefixed directive set. The leading <c>@</c> is part of the token (Pygments emits <c>@interface</c> as one keyword).</summary>
    private static readonly ByteKeywordSet AtDirectives = ByteKeywordSet.CreateFromSpaceSeparated(
        "interface implementation protocol end property synthesize dynamic class selector encode throw try catch finally autoreleasepool"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false YES NO NULL"u8);

    /// <summary>Operator alternation — shared C-style core (no ObjC additions over plain C).</summary>
    private static readonly byte[][] OperatorTable = CFamilyShared.StandardOperators;

    /// <summary>First-byte set for ObjC <c>@</c>-prefixed directives.</summary>
    private static readonly SearchValues<byte> AtDirectiveFirst = SearchValues.Create("@"u8);

    /// <summary>Gets the singleton Objective-C lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Objective-C lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = CFamilyShared.StandardPunctuation,
            IntegerSuffix = CFamilyShared.CIntegerSuffix,
            FloatSuffix = CFamilyShared.CFloatSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = true,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        var coreRules = CFamilyRules.Build(config);

        // Insert the @-directive rule near the front so `@interface` etc. classify as a single
        // declaration token before the punctuation rule consumes the bare `@`.
        var atDirectiveRule = new LexerRule(
            MatchAtDirective,
            TokenClass.KeywordDeclaration,
            LexerRule.NoStateChange) { FirstBytes = AtDirectiveFirst };

        var allRules = new LexerRule[coreRules.Length + 1];
        allRules[0] = atDirectiveRule;
        Array.Copy(coreRules, 0, allRules, 1, coreRules.Length);

        return new(LanguageRuleBuilder.BuildSingleState(allRules));
    }

    /// <summary>Matches an ObjC <c>@</c>-prefixed directive — <c>@</c> followed by an identifier that's a member of <see cref="AtDirectives"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the leading <c>@</c>), or zero.</returns>
    private static int MatchAtDirective(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'@')
        {
            return 0;
        }

        var idLen = TokenMatchers.MatchKeyword(slice[1..], AtDirectives);
        return idLen is 0 ? 0 : 1 + idLen;
    }
}
