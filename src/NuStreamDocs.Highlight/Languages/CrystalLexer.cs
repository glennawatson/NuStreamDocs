// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Crystal lexer.</summary>
/// <remarks>
/// Brace-style language with character literals; uses <c>#</c> for line
/// comments (wired through the special-string slot since C-family normally
/// uses <c>//</c>) and the Ruby-derived <c>def</c>/<c>class</c>/<c>module</c>/<c>end</c>
/// declaration shape.
/// </remarks>
public static class CrystalLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus Crystal-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "end"u8],
        [.. "begin"u8],
        [.. "rescue"u8],
        [.. "ensure"u8],
        [.. "raise"u8],
        [.. "next"u8],
        [.. "redo"u8],
        [.. "retry"u8],
        [.. "yield"u8],
        [.. "self"u8],
        [.. "super"u8],
        [.. "require"u8],
        [.. "in"u8],
        [.. "as"u8],
        [.. "is_a?"u8],
        [.. "responds_to?"u8],
        [.. "as?"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "when"u8],
        [.. "then"u8],
        [.. "of"u8],
        [.. "with"u8],
        [.. "out"u8]]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Bool"u8],
        [.. "Char"u8],
        [.. "Int8"u8],
        [.. "Int16"u8],
        [.. "Int32"u8],
        [.. "Int64"u8],
        [.. "UInt8"u8],
        [.. "UInt16"u8],
        [.. "UInt32"u8],
        [.. "UInt64"u8],
        [.. "Float32"u8],
        [.. "Float64"u8],
        [.. "String"u8],
        [.. "Symbol"u8],
        [.. "Array"u8],
        [.. "Hash"u8],
        [.. "Tuple"u8],
        [.. "NamedTuple"u8],
        [.. "Set"u8],
        [.. "Nil"u8]);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "def"u8],
        [.. "class"u8],
        [.. "struct"u8],
        [.. "module"u8],
        [.. "lib"u8],
        [.. "fun"u8],
        [.. "macro"u8],
        [.. "alias"u8],
        [.. "abstract"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "public"u8],
        [.. "enum"u8],
        [.. "type"u8],
        [.. "include"u8],
        [.. "extend"u8],
        [.. "annotation"u8],
        [.. "instance_sizeof"u8],
        [.. "sizeof"u8]);

    /// <summary>Constant keywords — shared <c>true</c>/<c>false</c> plus Crystal's <c>nil</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8]);

    /// <summary>Operator alternation — shared C-style core plus Crystal's range / heredoc-style forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "..."u8],
        [.. ".."u8],
        [.. "<=>"u8],
        [.. "==="u8],
        [.. "->"u8],
        [.. "=>"u8],
        [.. "::"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>Single-byte structural punctuation — Crystal uses <c>@</c> for instance variables and <c>:</c> for symbols.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@:"u8);

    /// <summary>First-byte set for the special-string slot — used here to wire the <c>#</c> line-comment rule.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>Gets the singleton Crystal lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Crystal lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        // Crystal uses # for line comments; the C-family helper recognizes // by default,
        // so we slot the Crystal comment through the special-string position (which fires
        // ahead of strings).
        var hashComment = new LexerRule(
            TokenMatchers.MatchHashComment,
            TokenClass.CommentSingle,
            LexerRule.NoStateChange) { FirstBytes = HashFirst };

        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = hashComment
        };

        return CFamilyRules.CreateLexer(config);
    }
}
