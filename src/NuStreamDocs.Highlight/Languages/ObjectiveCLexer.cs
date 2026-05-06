// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Objective-C lexer.</summary>
/// <remarks>
/// Brace-style language with C preprocessor and character literals; the Objective-C
/// <c>@interface</c> / <c>@implementation</c> / <c>@property</c> directives classify as
/// declaration keywords via the at-prefixed keyword table.
/// </remarks>
public static class ObjectiveCLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus the ObjC additions.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "goto"u8],
        [.. "sizeof"u8],
        [.. "typedef"u8],
        [.. "self"u8],
        [.. "super"u8],
        [.. "in"u8],
        [.. "out"u8],
        [.. "inout"u8],
        [.. "bycopy"u8],
        [.. "byref"u8],
        [.. "oneway"u8],
        [.. "nil"u8]]);

    /// <summary>Built-in primitive type keywords (C primitives plus ObjC <c>id</c> / <c>BOOL</c> / <c>SEL</c>).</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "char"u8],
        [.. "short"u8],
        [.. "int"u8],
        [.. "long"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "void"u8],
        [.. "signed"u8],
        [.. "unsigned"u8],
        [.. "id"u8],
        [.. "BOOL"u8],
        [.. "SEL"u8],
        [.. "Class"u8],
        [.. "IMP"u8],
        [.. "instancetype"u8],
        [.. "Protocol"u8],
        [.. "NSInteger"u8],
        [.. "NSUInteger"u8],
        [.. "NSString"u8],
        [.. "NSArray"u8],
        [.. "NSDictionary"u8]);

    /// <summary>Declaration / qualifier keywords (C-style; the ObjC <c>@interface</c>-style directives are handled separately).</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "auto"u8],
        [.. "register"u8],
        [.. "static"u8],
        [.. "extern"u8],
        [.. "const"u8],
        [.. "volatile"u8],
        [.. "inline"u8],
        [.. "restrict"u8],
        [.. "struct"u8],
        [.. "union"u8],
        [.. "enum"u8]);

    /// <summary>ObjC <c>@</c>-prefixed directive set. The leading <c>@</c> is part of the token (Pygments emits <c>@interface</c> as one keyword).</summary>
    private static readonly ByteKeywordSet AtDirectives = ByteKeywordSet.Create(
        [.. "interface"u8],
        [.. "implementation"u8],
        [.. "protocol"u8],
        [.. "end"u8],
        [.. "property"u8],
        [.. "synthesize"u8],
        [.. "dynamic"u8],
        [.. "class"u8],
        [.. "selector"u8],
        [.. "encode"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "autoreleasepool"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "YES"u8],
        [.. "NO"u8],
        [.. "NULL"u8]);

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
