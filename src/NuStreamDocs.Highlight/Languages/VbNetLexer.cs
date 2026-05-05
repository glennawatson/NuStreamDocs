// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>VB.NET lexer.</summary>
/// <remarks>
/// Case-insensitive C-family lexer driven by <see cref="CFamilyRules"/>; uses
/// <c>'</c> for line comments (handled as the single-string fallback because the
/// language has no character literals) and the standard <c>End</c>-block forms.
/// </remarks>
public static class VbNetLexer
{
    /// <summary>General-keyword set (case-insensitive — entries are lowercase).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateIgnoreCase(
        [.. "if"u8],
        [.. "then"u8],
        [.. "else"u8],
        [.. "elseif"u8],
        [.. "end"u8],
        [.. "select"u8],
        [.. "case"u8],
        [.. "for"u8],
        [.. "each"u8],
        [.. "next"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "loop"u8],
        [.. "until"u8],
        [.. "return"u8],
        [.. "exit"u8],
        [.. "continue"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "new"u8],
        [.. "me"u8],
        [.. "mybase"u8],
        [.. "myclass"u8],
        [.. "in"u8],
        [.. "to"u8],
        [.. "step"u8],
        [.. "as"u8],
        [.. "of"u8],
        [.. "is"u8],
        [.. "isnot"u8],
        [.. "with"u8],
        [.. "imports"u8],
        [.. "namespace"u8],
        [.. "yield"u8],
        [.. "await"u8],
        [.. "async"u8],
        [.. "handles"u8],
        [.. "addhandler"u8],
        [.. "removehandler"u8],
        [.. "raiseevent"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "andalso"u8],
        [.. "orelse"u8],
        [.. "xor"u8]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateIgnoreCase(
        [.. "boolean"u8],
        [.. "byte"u8],
        [.. "sbyte"u8],
        [.. "short"u8],
        [.. "ushort"u8],
        [.. "integer"u8],
        [.. "uinteger"u8],
        [.. "long"u8],
        [.. "ulong"u8],
        [.. "single"u8],
        [.. "double"u8],
        [.. "decimal"u8],
        [.. "char"u8],
        [.. "string"u8],
        [.. "object"u8],
        [.. "date"u8],
        [.. "datetime"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateIgnoreCase(
        [.. "dim"u8],
        [.. "const"u8],
        [.. "static"u8],
        [.. "shared"u8],
        [.. "readonly"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "friend"u8],
        [.. "internal"u8],
        [.. "overrides"u8],
        [.. "overridable"u8],
        [.. "mustoverride"u8],
        [.. "notoverridable"u8],
        [.. "shadows"u8],
        [.. "overloads"u8],
        [.. "default"u8],
        [.. "implements"u8],
        [.. "inherits"u8],
        [.. "interface"u8],
        [.. "class"u8],
        [.. "module"u8],
        [.. "structure"u8],
        [.. "enum"u8],
        [.. "delegate"u8],
        [.. "event"u8],
        [.. "property"u8],
        [.. "sub"u8],
        [.. "function"u8],
        [.. "operator"u8],
        [.. "byval"u8],
        [.. "byref"u8],
        [.. "optional"u8],
        [.. "paramarray"u8],
        [.. "partial"u8],
        [.. "mustinherit"u8],
        [.. "notinheritable"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateIgnoreCase(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nothing"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<="u8],
        [.. ">="u8],
        [.. "<>"u8],
        [.. "&="u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "&"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8]
    ];

    /// <summary>First-byte set for general keywords (lower + upper for case-insensitive dispatch).</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("acdefhilmnorstwxyACDEFHILMNORSTWXY"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("bcdiloOsubcDILOSUC"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("bcdefimnoprsuxBCDEFIMNOPRSUX"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfnTFN"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/&=<>"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){};,.:"u8);

    /// <summary>First-byte set for the special-string rule (single-quote line comment).</summary>
    private static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>Gets the singleton VB.NET lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the VB.NET lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        // VB.NET uses ' for line comments, not //. Wire it as the special-string slot.
        var lineComment = new LexerRule(
            static slice => TokenMatchers.MatchLineCommentToEol(slice, (byte)'\''),
            TokenClass.CommentSingle,
            LexerRule.NoStateChange) { FirstBytes = SingleQuoteFirst };

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
            OperatorFirst = OperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = lineComment
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
