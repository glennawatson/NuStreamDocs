// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Erlang lexer.</summary>
/// <remarks>
/// Custom shape — <c>%</c> line comments, lowercase atoms / variables-by-uppercase-leading,
/// <c>-module</c> / <c>-export</c> attributes, the standard <c>fun</c> / <c>case</c> /
/// <c>receive</c> control-flow keywords. Atom string syntax (<c>'with spaces'</c>) shares the
/// single-quoted-string matcher.
/// </remarks>
public static class ErlangLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "after"u8],
        [.. "and"u8],
        [.. "andalso"u8],
        [.. "band"u8],
        [.. "begin"u8],
        [.. "bnot"u8],
        [.. "bor"u8],
        [.. "bsl"u8],
        [.. "bsr"u8],
        [.. "bxor"u8],
        [.. "case"u8],
        [.. "catch"u8],
        [.. "cond"u8],
        [.. "div"u8],
        [.. "end"u8],
        [.. "fun"u8],
        [.. "if"u8],
        [.. "let"u8],
        [.. "not"u8],
        [.. "of"u8],
        [.. "or"u8],
        [.. "orelse"u8],
        [.. "receive"u8],
        [.. "rem"u8],
        [.. "try"u8],
        [.. "when"u8],
        [.. "xor"u8]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "integer"u8],
        [.. "float"u8],
        [.. "atom"u8],
        [.. "binary"u8],
        [.. "boolean"u8],
        [.. "list"u8],
        [.. "tuple"u8],
        [.. "map"u8],
        [.. "pid"u8],
        [.. "port"u8],
        [.. "reference"u8],
        [.. "string"u8]);

    /// <summary>Declaration / module-attribute keywords (matched after the leading <c>-</c> via the dash-attribute rule).</summary>
    private static readonly ByteKeywordSet ModuleAttributes = ByteKeywordSet.Create(
        [.. "module"u8],
        [.. "export"u8],
        [.. "import"u8],
        [.. "behaviour"u8],
        [.. "behavior"u8],
        [.. "compile"u8],
        [.. "include"u8],
        [.. "include_lib"u8],
        [.. "define"u8],
        [.. "ifdef"u8],
        [.. "ifndef"u8],
        [.. "endif"u8],
        [.. "spec"u8],
        [.. "type"u8],
        [.. "callback"u8],
        [.. "record"u8],
        [.. "vsn"u8],
        [.. "author"u8],
        [.. "on_load"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8]);

    /// <summary>Operator alternation.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "=:="u8],
        [.. "=/="u8],
        [.. "->"u8],
        [.. "<-"u8],
        [.. "++"u8],
        [.. "--"u8],
        [.. "=="u8],
        [.. "/="u8],
        [.. "=<"u8],
        [.. ">="u8],
        [.. "||"u8],
        [.. "::"u8],
        [.. "!"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8]
    ];

    /// <summary>First-byte set for whitespace runs.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the <c>%</c> line-comment rule.</summary>
    private static readonly SearchValues<byte> PercentFirst = SearchValues.Create("%"u8);

    /// <summary>First-byte set for the dash-attribute rule (<c>-module</c>, <c>-export</c>, …).</summary>
    private static readonly SearchValues<byte> DashFirst = SearchValues.Create("-"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefilnorxtw"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abfilmprt s"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tf"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("=<>!+-*/|:"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.#"u8);

    /// <summary>Identifier-continuation set — Erlang allows <c>@</c> in atom names.</summary>
    private static readonly SearchValues<byte> AtomContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_@"u8);

    /// <summary>Variable-start byte set — Erlang variables begin with an uppercase letter or underscore.</summary>
    private static readonly SearchValues<byte> VariableStart = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ_"u8);

    /// <summary>Atom-start byte set — Erlang atoms begin with a lowercase letter.</summary>
    private static readonly SearchValues<byte> AtomStart = SearchValues.Create(
        "abcdefghijklmnopqrstuvwxyz"u8);

    /// <summary>Gets the singleton Erlang lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        WhitespaceFirst = WhitespaceFirst,
        LineComment = new(static slice => TokenMatchers.MatchLineCommentToEol(slice, (byte)'%'), TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = PercentFirst },
        PreCommentRule = new(MatchDashAttribute, TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = DashFirst, RequiresLineStart = true },
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = KeywordConstants,
        KeywordConstantFirst = KeywordConstantFirst,
        KeywordTypes = KeywordTypes,
        KeywordTypeFirst = KeywordTypeFirst,
        Keywords = Keywords,
        KeywordFirst = KeywordFirst,
        ExtraRules =
        [
            new(MatchVariable, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = VariableStart },
            new(MatchAtom, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = AtomStart }
        ],
        SuppressIdentifierRule = true,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

    /// <summary>Matches a leading <c>-attribute</c> at the start of a line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (dash + identifier), or zero.</returns>
    private static int MatchDashAttribute(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 2 || slice[0] is not (byte)'-')
        {
            return 0;
        }

        var idLen = TokenMatchers.MatchKeyword(slice[1..], ModuleAttributes);
        return idLen is 0 ? 0 : 1 + idLen;
    }

    /// <summary>Matches an Erlang variable — uppercase or underscore start, then identifier-continue bytes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchVariable(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchIdentifier(slice, VariableStart, AtomContinue);

    /// <summary>Matches an Erlang atom — lowercase start, then identifier-continue bytes (including <c>@</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchAtom(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchIdentifier(slice, AtomStart, AtomContinue);
}
