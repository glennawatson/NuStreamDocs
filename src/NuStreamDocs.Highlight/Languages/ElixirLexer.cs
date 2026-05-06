// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Elixir lexer.</summary>
/// <remarks>
/// Custom shape — <c>#</c> line comments, <c>defmodule</c> / <c>def</c> /
/// <c>defp</c> / <c>defmacro</c> declarations, <c>:atom</c> literals, <c>~r</c> / <c>~w</c>
/// / <c>~s</c> sigils, and the standard control-flow keywords. Module attributes
/// (<c>@moduledoc</c>, <c>@doc</c>, <c>@spec</c>) classify as a single name token.
/// </remarks>
public static class ElixirLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "unless"u8],
        [.. "else"u8],
        [.. "end"u8],
        [.. "case"u8],
        [.. "cond"u8],
        [.. "do"u8],
        [.. "for"u8],
        [.. "with"u8],
        [.. "when"u8],
        [.. "in"u8],
        [.. "fn"u8],
        [.. "receive"u8],
        [.. "try"u8],
        [.. "rescue"u8],
        [.. "catch"u8],
        [.. "after"u8],
        [.. "raise"u8],
        [.. "throw"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "import"u8],
        [.. "alias"u8],
        [.. "require"u8],
        [.. "use"u8],
        [.. "quote"u8],
        [.. "unquote"u8]);

    /// <summary>Declaration / module keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "defmodule"u8],
        [.. "defprotocol"u8],
        [.. "defimpl"u8],
        [.. "defstruct"u8],
        [.. "def"u8],
        [.. "defp"u8],
        [.. "defmacro"u8],
        [.. "defmacrop"u8],
        [.. "defguard"u8],
        [.. "defguardp"u8],
        [.. "defcallback"u8],
        [.. "defdelegate"u8],
        [.. "defexception"u8],
        [.. "defoverridable"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8]);

    /// <summary>Operator alternation.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "==="u8],
        [.. "!=="u8],
        [.. "<>"u8],
        [.. "|>"u8],
        [.. "->"u8],
        [.. "=>"u8],
        [.. "::"u8],
        [.. "++"u8],
        [.. "--"u8],
        [.. "**"u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. ".."u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "&"u8],
        [.. "|"u8],
        [.. "^"u8],
        [.. "!"u8],
        [.. "~"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8]
    ];

    /// <summary>First-byte set for the <c>#</c> line-comment rule.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the <c>:atom</c> literal rule.</summary>
    private static readonly SearchValues<byte> ColonFirst = SearchValues.Create(":"u8);

    /// <summary>First-byte set for the <c>@attribute</c> module-attribute rule.</summary>
    private static readonly SearchValues<byte> AtFirst = SearchValues.Create("@"u8);

    /// <summary>First-byte set for the <c>~sigil</c> rule.</summary>
    private static readonly SearchValues<byte> TildeFirst = SearchValues.Create("~"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,."u8);

    /// <summary>Gets the singleton Elixir lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        PreCommentRule = new(MatchModuleAttribute, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = AtFirst },
        LineComment = new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
        SpecialString = new(MatchSigil, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = TildeFirst },
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        PostStringRules = [new(MatchAtom, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = ColonFirst }],
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = KeywordConstants,
        KeywordDeclarations = KeywordDeclarations,
        Keywords = Keywords,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

    /// <summary>Matches a <c>:atom</c> literal — colon then identifier body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchAtom(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 2 || slice[0] is not (byte)':' || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var ident = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        return ident is 0 ? 0 : 1 + ident;
    }

    /// <summary>Matches a <c>@attribute</c> module-attribute reference.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchModuleAttribute(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 2 || slice[0] is not (byte)'@' || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var ident = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        return ident is 0 ? 0 : 1 + ident;
    }

    /// <summary>Matches a Elixir sigil — <c>~name</c> followed by a delimiter pair (one of <c>/.../</c>, <c>(...)</c>, <c>[...]</c>, <c>{...}</c>, <c>|...|</c>, <c>"..."</c>, <c>'...'</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchSigil(ReadOnlySpan<byte> slice)
    {
        const int MinSigilLength = 4;
        if (slice.Length < MinSigilLength || slice[0] is not (byte)'~')
        {
            return 0;
        }

        if (!TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var close = SigilCloseFor(slice[2]);
        return close is 0 ? 0 : ScanSigilBody(slice, close);
    }

    /// <summary>Returns the matching close byte for a sigil opener, or <c>0</c> if the byte isn't a recognized sigil delimiter.</summary>
    /// <param name="open">Opener byte at <c>slice[2]</c>.</param>
    /// <returns>Matching close byte, or zero.</returns>
    private static byte SigilCloseFor(byte open) => open switch
    {
        (byte)'(' => (byte)')',
        (byte)'[' => (byte)']',
        (byte)'{' => (byte)'}',
        (byte)'<' => (byte)'>',
        (byte)'/' => (byte)'/',
        (byte)'|' => (byte)'|',
        (byte)'"' => (byte)'"',
        (byte)'\'' => (byte)'\'',
        _ => 0
    };

    /// <summary>Walks the sigil body until the matching <paramref name="close"/>, then consumes optional modifier flags.</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="close">Matching close byte.</param>
    /// <returns>Total length matched on success, zero on unterminated input.</returns>
    private static int ScanSigilBody(ReadOnlySpan<byte> slice, byte close)
    {
        const int BodyStart = 3;
        const int BackslashEscapeAdvance = 2;
        var pos = BodyStart;
        while (pos < slice.Length)
        {
            if (slice[pos] is (byte)'\\' && pos + 1 < slice.Length)
            {
                pos += BackslashEscapeAdvance;
                continue;
            }

            if (slice[pos] == close)
            {
                pos++;
                while (pos < slice.Length && TokenMatchers.AsciiIdentifierStart.Contains(slice[pos]))
                {
                    pos++;
                }

                return pos;
            }

            pos++;
        }

        return 0;
    }
}
