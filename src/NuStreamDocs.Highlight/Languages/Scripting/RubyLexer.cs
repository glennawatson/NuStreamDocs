// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Scripting;

/// <summary>Ruby lexer.</summary>
/// <remarks>
/// Pragmatic single-state Ruby lexer. Custom shape — Ruby uses
/// <c>#</c> line comments, <c>=begin</c>/<c>=end</c> block comments,
/// sigils for instance / class / global variables (<c>@</c>, <c>@@</c>,
/// <c>$</c>), and symbol literals (<c>:name</c>). String interpolation
/// (<c>"#{expr}"</c>) is folded into the string body — themes still
/// colour the literal, and the inner expression isn't re-entered without
/// a state stack. Heredoc bodies aren't fully tracked; the introducer
/// classifies as a string token and the body falls through as text.
/// </remarks>
public static class RubyLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "unless"u8],
        [.. "elsif"u8],
        [.. "else"u8],
        [.. "end"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "until"u8],
        [.. "do"u8],
        [.. "case"u8],
        [.. "when"u8],
        [.. "then"u8],
        [.. "in"u8],
        [.. "break"u8],
        [.. "next"u8],
        [.. "redo"u8],
        [.. "retry"u8],
        [.. "return"u8],
        [.. "yield"u8],
        [.. "begin"u8],
        [.. "rescue"u8],
        [.. "ensure"u8],
        [.. "raise"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "self"u8],
        [.. "super"u8],
        [.. "loop"u8],
        [.. "lambda"u8],
        [.. "proc"u8],
        [.. "require"u8],
        [.. "require_relative"u8],
        [.. "load"u8],
        [.. "include"u8],
        [.. "extend"u8],
        [.. "puts"u8],
        [.. "print"u8],
        [.. "p"u8],
        [.. "pp"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "def"u8],
        [.. "class"u8],
        [.. "module"u8],
        [.. "attr_accessor"u8],
        [.. "attr_reader"u8],
        [.. "attr_writer"u8],
        [.. "private"u8],
        [.. "public"u8],
        [.. "protected"u8],
        [.. "alias"u8],
        [.. "alias_method"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8],
        [.. "__FILE__"u8],
        [.. "__LINE__"u8],
        [.. "__dir__"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<=>"u8],
        [.. "**"u8],
        [.. "=="u8],
        [.. "==="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "=~"u8],
        [.. "!~"u8],
        [.. ".."u8],
        [.. "..."u8],
        [.. "->"u8],
        [.. "=>"u8],
        [.. "::"u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
        [.. "&="u8],
        [.. "|="u8],
        [.. "^="u8],
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
        [.. ">"u8],
        [.. "?"u8]
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abceiflmnoprstuwy"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acdmp"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn_"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?:."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,."u8);

    /// <summary>First-byte set for the variable / sigil rule.</summary>
    private static readonly SearchValues<byte> SigilFirst = SearchValues.Create("@$"u8);

    /// <summary>First-byte set for the symbol-literal rule.</summary>
    private static readonly SearchValues<byte> ColonFirst = SearchValues.Create(":"u8);

    /// <summary>First-byte set for the hash-comment rule.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the equal-block-comment rule (<c>=begin</c>/<c>=end</c>).</summary>
    private static readonly SearchValues<byte> EqualFirst = SearchValues.Create("="u8);

    /// <summary>Whitespace dispatch set.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>Gets the singleton Ruby lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        WhitespaceFirst = WhitespaceFirst,
        PreCommentRule = new(MatchEqualBlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = EqualFirst, RequiresLineStart = true },
        LineComment = new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        PostStringRules =
        [
            new(MatchSymbol, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = ColonFirst },
            new(MatchSigilVariable, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = SigilFirst }
        ],
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = KeywordConstants,
        KeywordConstantFirst = KeywordConstantFirst,
        KeywordDeclarations = KeywordDeclarations,
        KeywordDeclarationFirst = KeywordDeclarationFirst,
        Keywords = Keywords,
        KeywordFirst = KeywordFirst,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

    /// <summary>Matches a Ruby <c>=begin ... =end</c> block comment, line-anchored.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchEqualBlockComment(ReadOnlySpan<byte> slice)
    {
        if (!slice.StartsWith("=begin"u8))
        {
            return 0;
        }

        var endMarker = slice.IndexOf("\n=end"u8);
        if (endMarker < 0)
        {
            return 0;
        }

        // The "\n=end" needle is five bytes — newline + the four-byte =end token.
        const int NewlineEndMarkerLength = 5;
        var afterEnd = endMarker + NewlineEndMarkerLength;
        if (afterEnd >= slice.Length)
        {
            return afterEnd;
        }

        // Consume any trailing characters on the =end line (per Ruby's spec) — but
        // stop before the next newline so the comment span doesn't include the
        // following line.
        return afterEnd + TokenMatchers.LineLength(slice[afterEnd..]);
    }

    /// <summary>Matches a Ruby symbol literal (<c>:name</c>) — colon followed by an identifier body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchSymbol(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 2 || slice[0] is not (byte)':' || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var idLen = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        return idLen is 0 ? 0 : 1 + idLen;
    }

    /// <summary>Matches a Ruby sigil variable: <c>@var</c>, <c>@@var</c>, or <c>$var</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (sigil + identifier), or zero.</returns>
    private static int MatchSigilVariable(ReadOnlySpan<byte> slice)
    {
        if (slice is [])
        {
            return 0;
        }

        var pos = 0;
        if (slice[pos] is (byte)'@')
        {
            pos++;
            if (pos < slice.Length && slice[pos] is (byte)'@')
            {
                pos++;
            }
        }
        else if (slice[pos] is (byte)'$')
        {
            pos++;
        }
        else
        {
            return 0;
        }

        var idLen = TokenMatchers.MatchAsciiIdentifier(slice[pos..]);
        return idLen is 0 ? 0 : pos + idLen;
    }
}
