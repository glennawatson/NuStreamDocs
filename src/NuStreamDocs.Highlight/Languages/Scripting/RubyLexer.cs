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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "if unless elsif else end for while until do case when then in break next redo retry return yield"u8,
        "begin rescue ensure raise and or not self super loop lambda proc require require_relative load include extend puts print p pp"u8);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "def class module attr_accessor attr_reader attr_writer private public protected alias alias_method"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false nil __FILE__ __LINE__ __dir__"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "<=> === ** == != <= >= && || =~ !~ ... .. -> => :: += -= *= /= %= &= |= ^= + - * / % & | ^ ! ~ = < > ?"u8);

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
