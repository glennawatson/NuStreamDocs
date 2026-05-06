// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>R lexer.</summary>
/// <remarks>
/// <c>#</c> line comments, <c>&lt;-</c> assignment operator, <c>function</c>
/// declarations, and the standard control-flow keywords. Identifiers may
/// contain dots (<c>data.frame</c>, <c>read.csv</c>) — handled via a wider
/// continuation set.
/// </remarks>
public static class RLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "else"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "repeat"u8],
        [.. "break"u8],
        [.. "next"u8],
        [.. "return"u8],
        [.. "in"u8],
        [.. "library"u8],
        [.. "require"u8],
        [.. "source"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "function"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "TRUE"u8],
        [.. "FALSE"u8],
        [.. "NULL"u8],
        [.. "NA"u8],
        [.. "NaN"u8],
        [.. "Inf"u8],
        [.. "T"u8],
        [.. "F"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "%/%"u8],
        [.. "%%"u8],
        [.. "%*%"u8],
        [.. "%in%"u8],
        [.. "<<-"u8],
        [.. "->>"u8],
        [.. "<-"u8],
        [.. "->"u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. ":="u8],
        [.. "**"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "^"u8],
        [.. "&"u8],
        [.. "|"u8],
        [.. "!"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "?"u8],
        [.. "~"u8],
        [.. ":"u8]
    ];

    /// <summary>Identifier-continuation set — letters, digits, underscore, dot (R's identifier convention).</summary>
    private static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_."u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/^&|!=<>?:%~"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,$@"u8);

    /// <summary>Gets the singleton R lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        LineComment = new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = SearchValues.Create("#"u8) },
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = KeywordConstants,
        KeywordDeclarations = KeywordDeclarations,
        Keywords = Keywords,
        IdentifierContinue = IdentifierContinue,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });
}
