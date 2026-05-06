// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Scripting;

/// <summary>Perl lexer.</summary>
/// <remarks>
/// Byte-level Perl scanner. Recognizes sigil variables (<c>$x</c>, <c>@x</c>,
/// <c>%x</c>), the standard quote-like operators (<c>q{}</c>, <c>qq{}</c>,
/// <c>qw{}</c>, <c>qr{}</c>, <c>m{}</c>) folded into a single string-like token,
/// POD blocks (<c>=pod</c> … <c>=cut</c>), heredoc introducers, and the
/// usual control-flow / declaration keyword set. The inner content of regex /
/// substitution literals isn't parsed — themes still get a string-shaped
/// classification on the whole literal. Heredoc bodies are left as plain text
/// (the introducer line classifies as a string; the body bytes pass through).
/// </remarks>
public static class PerlLexer
{
    /// <summary>Length of a one-byte quote-like operator prefix (<c>q</c>, <c>m</c>, <c>s</c>, <c>y</c>).</summary>
    private const int OneBytePrefix = 1;

    /// <summary>Length of a two-byte quote-like operator prefix (<c>qq</c>, <c>qw</c>, <c>qr</c>, <c>tr</c>).</summary>
    private const int TwoBytePrefix = 2;

    /// <summary>General-keyword set (control-flow + word operators).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "elsif"u8],
        [.. "else"u8],
        [.. "unless"u8],
        [.. "while"u8],
        [.. "until"u8],
        [.. "for"u8],
        [.. "foreach"u8],
        [.. "do"u8],
        [.. "last"u8],
        [.. "next"u8],
        [.. "redo"u8],
        [.. "return"u8],
        [.. "goto"u8],
        [.. "die"u8],
        [.. "warn"u8],
        [.. "eval"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "xor"u8],
        [.. "eq"u8],
        [.. "ne"u8],
        [.. "lt"u8],
        [.. "gt"u8],
        [.. "le"u8],
        [.. "ge"u8],
        [.. "cmp"u8]);

    /// <summary>Declaration / scope keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "sub"u8],
        [.. "package"u8],
        [.. "use"u8],
        [.. "no"u8],
        [.. "require"u8],
        [.. "my"u8],
        [.. "our"u8],
        [.. "local"u8],
        [.. "state"u8],
        [.. "BEGIN"u8],
        [.. "END"u8],
        [.. "INIT"u8],
        [.. "CHECK"u8],
        [.. "UNITCHECK"u8]);

    /// <summary>Common Perl built-ins (subset covering the highest-frequency calls).</summary>
    private static readonly ByteKeywordSet Builtins = ByteKeywordSet.Create(
        [.. "print"u8],
        [.. "printf"u8],
        [.. "say"u8],
        [.. "sprintf"u8],
        [.. "chomp"u8],
        [.. "chop"u8],
        [.. "split"u8],
        [.. "join"u8],
        [.. "length"u8],
        [.. "lc"u8],
        [.. "uc"u8],
        [.. "lcfirst"u8],
        [.. "ucfirst"u8],
        [.. "reverse"u8],
        [.. "sort"u8],
        [.. "grep"u8],
        [.. "map"u8],
        [.. "ref"u8],
        [.. "defined"u8],
        [.. "exists"u8],
        [.. "delete"u8],
        [.. "keys"u8],
        [.. "values"u8],
        [.. "scalar"u8],
        [.. "wantarray"u8],
        [.. "open"u8],
        [.. "close"u8],
        [.. "read"u8],
        [.. "write"u8],
        [.. "binmode"u8],
        [.. "bless"u8],
        [.. "shift"u8],
        [.. "unshift"u8],
        [.. "push"u8],
        [.. "pop"u8],
        [.. "splice"u8]);

    /// <summary>Constants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "undef"u8],
        [.. "__FILE__"u8],
        [.. "__LINE__"u8],
        [.. "__PACKAGE__"u8],
        [.. "__SUB__"u8],
        [.. "__DATA__"u8],
        [.. "__END__"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<=>"u8],
        [.. "**="u8],
        [.. "&&="u8],
        [.. "||="u8],
        [.. "//="u8],
        [.. "<<="u8],
        [.. ">>="u8],
        [.. "=~"u8],
        [.. "!~"u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "//"u8],
        [.. "**"u8],
        [.. "->"u8],
        [.. "=>"u8],
        [.. "::"u8],
        [.. ".."u8],
        [.. "++"u8],
        [.. "--"u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. ".="u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "."u8],
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

    /// <summary>First-byte set for whitespace.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the <c>#</c> comment dispatch.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the POD block (<c>=</c> at line start).</summary>
    private static readonly SearchValues<byte> EqualsFirst = SearchValues.Create("="u8);

    /// <summary>First-byte set for the heredoc-introducer rule (<c>&lt;&lt;</c>).</summary>
    private static readonly SearchValues<byte> AngleAngleFirst = SearchValues.Create("<"u8);

    /// <summary>First-byte set for sigil variables (<c>$</c>, <c>@</c>, <c>%</c>, <c>&amp;</c>).</summary>
    private static readonly SearchValues<byte> SigilFirst = SearchValues.Create("$@%&"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("acdefgilnoruwx"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("BCEINUlmnopqrsu"u8);

    /// <summary>First-byte set for builtins.</summary>
    private static readonly SearchValues<byte> BuiltinFirst = SearchValues.Create("bcdegjklmoprsuvw"u8);

    /// <summary>First-byte set for constants.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("u_"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?:."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,"u8);

    /// <summary>Hex-literal body bytes (digits + underscore separator).</summary>
    private static readonly SearchValues<byte> HexBody = SearchValues.Create("0123456789abcdefABCDEF_"u8);

    /// <summary>Set of bytes that introduce a sigil variable's name (after the sigil).</summary>
    private static readonly SearchValues<byte> SpecialVariableBytes = SearchValues.Create(
        "0123456789_/\\!@$%^&*()-+=`~:;.,?<>'\""u8);

    /// <summary>Quote-like operator first-byte set — <c>q</c>, <c>m</c>, <c>s</c>, <c>y</c>, <c>t</c> (for <c>tr</c>).</summary>
    private static readonly SearchValues<byte> QuoteOperatorFirst = SearchValues.Create("qmsty"u8);

    /// <summary>First-byte set for the backtick-string rule.</summary>
    private static readonly SearchValues<byte> BacktickFirst = SearchValues.Create("`"u8);

    /// <summary>Gets the singleton Perl lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        WhitespaceFirst = WhitespaceFirst,
        PreCommentRule = new(MatchPodBlock, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = EqualsFirst, RequiresLineStart = true },
        LineComment = new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        PostStringRules =
        [
            new(MatchHeredocIntroducer, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = AngleAngleFirst },
            new(MatchQuoteOperator, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = QuoteOperatorFirst },
            new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'`'), TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = BacktickFirst },
            new(MatchSigilVariable, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = SigilFirst },
            new(MatchHexLiteral, TokenClass.NumberHex, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.HexFirst }
        ],
        IncludeFloatLiteral = true,
        IncludeIntegerLiteral = true,
        KeywordConstants = KeywordConstants,
        KeywordConstantFirst = KeywordConstantFirst,
        KeywordDeclarations = KeywordDeclarations,
        KeywordDeclarationFirst = KeywordDeclarationFirst,
        Keywords = Keywords,
        KeywordFirst = KeywordFirst,
        BuiltinKeywords = Builtins,
        BuiltinKeywordFirst = BuiltinFirst,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

    /// <summary>Matches a POD block — <c>=word</c> at line start through to a matching <c>=cut</c> on its own line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchPodBlock(ReadOnlySpan<byte> slice)
    {
        // Must be `=` immediately followed by an ASCII letter (so the assignment operator `=` and the
        // comparison `==` don't trigger).
        if (slice.Length < 2 || slice[0] is not (byte)'=' || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        // Walk until "\n=cut" then consume to end of that line.
        var endMarker = slice.IndexOf("\n=cut"u8);
        if (endMarker < 0)
        {
            return 0;
        }

        const int NewlineCutLength = 5;
        var afterEnd = endMarker + NewlineCutLength;
        if (afterEnd >= slice.Length)
        {
            return afterEnd;
        }

        return afterEnd + TokenMatchers.LineLength(slice[afterEnd..]);
    }

    /// <summary>
    /// Matches a heredoc introducer — <c>&lt;&lt;TAG</c>, <c>&lt;&lt;"TAG"</c>, <c>&lt;&lt;'TAG'</c>,
    /// or <c>&lt;&lt;~TAG</c>. Only the introducer text is consumed; the body bytes pass through
    /// as plain text.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchHeredocIntroducer(ReadOnlySpan<byte> slice)
    {
        const int DoubleAngleLength = 2;
        if (slice.Length < DoubleAngleLength + 1 || slice[0] is not (byte)'<' || slice[1] is not (byte)'<')
        {
            return 0;
        }

        // Optional ~ for indented heredoc (Perl 5.26+).
        var pos = DoubleAngleLength;
        if (pos < slice.Length && slice[pos] is (byte)'~')
        {
            pos++;
        }

        var tagLen = MatchHeredocTag(slice[pos..]);
        return tagLen is 0 ? 0 : pos + tagLen;
    }

    /// <summary>Matches the heredoc tag — quoted (<c>'TAG'</c> / <c>"TAG"</c>) or bare ASCII-identifier form.</summary>
    /// <param name="slice">Slice anchored after the optional <c>~</c> indented marker.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchHeredocTag(ReadOnlySpan<byte> slice)
    {
        if (slice is [])
        {
            return 0;
        }

        if (slice[0] is (byte)'\'' or (byte)'"')
        {
            return TokenMatchers.MatchQuotedWithBackslashEscape(slice, slice[0]);
        }

        return TokenMatchers.AsciiIdentifierStart.Contains(slice[0])
            ? TokenMatchers.MatchAsciiIdentifier(slice)
            : 0;
    }

    /// <summary>Matches a quote-like operator — <c>q</c>, <c>qq</c>, <c>qw</c>, <c>qr</c>, <c>m</c>, <c>s</c>, <c>tr</c>, <c>y</c> followed by a delimited body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchQuoteOperator(ReadOnlySpan<byte> slice)
    {
        var prefixLen = ConsumeQuoteOperatorPrefix(slice);
        if (prefixLen is 0
            || prefixLen >= slice.Length
            || TokenMatchers.AsciiIdentifierContinue.Contains(slice[prefixLen]))
        {
            return 0;
        }

        var open = slice[prefixLen];
        var close = MatchingClose(open);
        var firstBodyEnd = ScanQuoteBodyTail(slice, prefixLen + 1, open, close);
        if (firstBodyEnd is 0)
        {
            return 0;
        }

        var pos = firstBodyEnd;
        if (!IsTwoArgQuoteOperator(slice[..prefixLen]))
        {
            return pos + ConsumeModifierFlags(slice[pos..]);
        }

        pos = ContinueSecondQuoteBody(slice, pos, open, close);
        return pos is 0 ? 0 : pos + ConsumeModifierFlags(slice[pos..]);
    }

    /// <summary>Consumes the second delimited body of a two-arg quote operator (<c>s/from/to/</c>, <c>tr/from/to/</c>, <c>y/from/to/</c>).</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="afterFirstBody">Index past the first body's closing delimiter.</param>
    /// <param name="open">Opener byte from the first body.</param>
    /// <param name="close">Matching close byte from the first body.</param>
    /// <returns>Index past the second body's closing delimiter, or zero on miss.</returns>
    private static int ContinueSecondQuoteBody(ReadOnlySpan<byte> slice, int afterFirstBody, byte open, byte close)
    {
        // Paired-bracket forms need their own opener byte for the second body; symmetric-delimiter
        // forms reuse the same byte as the first body's terminator.
        if (open == close)
        {
            return ScanQuoteBodyTail(slice, afterFirstBody, open, close);
        }

        return afterFirstBody >= slice.Length || slice[afterFirstBody] != open
            ? 0
            : ScanQuoteBodyTail(slice, afterFirstBody + 1, open, close);
    }

    /// <summary>Consumes optional trailing modifier flags (<c>i</c>, <c>m</c>, <c>s</c>, <c>x</c>, <c>g</c>, <c>e</c>, <c>o</c>, <c>n</c>, <c>p</c>, <c>r</c>) following a quote-like body.</summary>
    /// <param name="slice">Slice anchored after the quote-body's closer.</param>
    /// <returns>Number of modifier-flag bytes consumed.</returns>
    private static int ConsumeModifierFlags(ReadOnlySpan<byte> slice)
    {
        var pos = 0;
        while (pos < slice.Length && TokenMatchers.AsciiIdentifierStart.Contains(slice[pos]))
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Matches a <c>0x...</c> hex literal with optional underscore digit separators.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchHexLiteral(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, CFamilyRules.NoSuffix);

    /// <summary>Consumes the recognized prefix of a quote-like operator (<c>q</c>, <c>qq</c>, <c>qw</c>, <c>qr</c>, <c>m</c>, <c>s</c>, <c>tr</c>, <c>y</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Prefix byte count, or zero on miss.</returns>
    private static int ConsumeQuoteOperatorPrefix(ReadOnlySpan<byte> slice) => slice switch
    {
        [(byte)'q', (byte)'q', ..] => TwoBytePrefix,
        [(byte)'q', (byte)'w', ..] => TwoBytePrefix,
        [(byte)'q', (byte)'r', ..] => TwoBytePrefix,
        [(byte)'q', ..] => OneBytePrefix,
        [(byte)'m', ..] => OneBytePrefix,
        [(byte)'s', ..] => OneBytePrefix,
        [(byte)'t', (byte)'r', ..] => TwoBytePrefix,
        [(byte)'y', ..] => OneBytePrefix,
        _ => 0
    };

    /// <summary>Returns true when the prefix is <c>s</c>, <c>tr</c>, or <c>y</c> — the operators that take two delimited bodies.</summary>
    /// <param name="prefix">Prefix bytes.</param>
    /// <returns>True for two-argument forms.</returns>
    private static bool IsTwoArgQuoteOperator(ReadOnlySpan<byte> prefix) =>
        prefix is [(byte)'s']
            or [(byte)'t', (byte)'r']
            or [(byte)'y'];

    /// <summary>Returns the matching close byte for an opener — paired brackets get their pair, other bytes match themselves.</summary>
    /// <param name="open">Opener byte.</param>
    /// <returns>Matching close byte.</returns>
    private static byte MatchingClose(byte open) => open switch
    {
        (byte)'(' => (byte)')',
        (byte)'[' => (byte)']',
        (byte)'{' => (byte)'}',
        (byte)'<' => (byte)'>',
        _ => open
    };

    /// <summary>Backslash-aware body-scan helper used for both the first and second arms of quote-like operators (paired-bracket and symmetric-delimiter forms).</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="bodyStart">Index of the first body byte.</param>
    /// <param name="open">Opener byte (used for nested-bracket tracking when paired with a different <paramref name="close"/>).</param>
    /// <param name="close">Matching close byte.</param>
    /// <returns>Total length matched on success (including the closing delimiter), zero on unterminated input.</returns>
    private static int ScanQuoteBodyTail(ReadOnlySpan<byte> slice, int bodyStart, byte open, byte close)
    {
        const int BackslashAdvance = 2;
        var depth = 1;
        var pos = bodyStart;
        while (pos < slice.Length)
        {
            var b = slice[pos];
            if (b is (byte)'\\' && pos + 1 < slice.Length)
            {
                pos += BackslashAdvance;
                continue;
            }

            if (open != close && b == open)
            {
                depth++;
            }
            else if (b == close)
            {
                depth--;
                if (depth is 0)
                {
                    return pos + 1;
                }
            }

            pos++;
        }

        return 0;
    }

    /// <summary>
    /// Matches a Perl sigil variable — <c>$name</c>, <c>@name</c>, <c>%name</c>, <c>&amp;name</c>,
    /// plus the special single-byte forms (<c>$_</c>, <c>$1</c>, <c>$@</c>, <c>$$</c>, …).
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (sigil + body), or zero.</returns>
    private static int MatchSigilVariable(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 2)
        {
            return 0;
        }

        if (slice[0] is not ((byte)'$' or (byte)'@' or (byte)'%' or (byte)'&'))
        {
            return 0;
        }

        // ${...} or @{...} dereference / interpolation form.
        if (slice[1] is (byte)'{')
        {
            var bracket = TokenMatchers.MatchBracketedBlock(slice[1..], (byte)'{', (byte)'}');
            return bracket is 0 ? 0 : 1 + bracket;
        }

        // Regular identifier name.
        var idLen = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        if (idLen > 0)
        {
            return 1 + idLen;
        }

        // Special single-byte names ($_, $1, $@, $$, $!, …).
        const int SigilPlusOneByte = 2;
        return SpecialVariableBytes.Contains(slice[1]) ? SigilPlusOneByte : 0;
    }
}
