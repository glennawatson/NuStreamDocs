// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Python rule list factory.</summary>
/// <remarks>
/// Pragmatic single-state port of Pygments' <c>PythonLexer</c>. The
/// Pygments grammar is multi-state — separate states for triple-quoted
/// strings, f-string interpolation, and the dotted-path lookup after
/// <c>from</c> / <c>import</c>. We collapse to one state by:
/// <list type="bullet">
/// <item><description>Triple-quoted <c>"""..."""</c> / <c>'''...'''</c> consumed as a single
///   token via dedicated bytewise matchers (newlines included).</description></item>
/// <item><description>String prefixes (<c>r</c>, <c>b</c>, <c>f</c>, <c>u</c>, and any
///   case / order combination) are matched as part of the literal —
///   f-string <c>{expr}</c> bodies stay inside the string token, which is
///   the same trade-off Pygments accepts when the inner expression
///   can't be re-entered without a state stack.</description></item>
/// <item><description><c>@decorator</c> consumed as a single name token so themes can
///   render the whole sigil + dotted path with the decorator colour.</description></item>
/// </list>
/// </remarks>
internal static class PythonRules
{
    /// <summary>Minimum opening / closing quote run for a triple-quoted string literal.</summary>
    private const int TripleQuoteLength = 3;

    /// <summary>Combined length of the opening and closing triple-quote runs.</summary>
    private const int TripleQuoteOpenAndCloseLength = TripleQuoteLength * 2;

    /// <summary>Index of the third byte in a triple-quote run (used as a slice index, not a magic count).</summary>
    private const int TripleQuoteThirdByteIndex = 2;

    /// <summary>Control-flow / declaration keywords from Pygments' <c>PythonLexer.tokens['keywords']</c>.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        "assert",
        "async",
        "await",
        "break",
        "case",
        "class",
        "continue",
        "def",
        "del",
        "elif",
        "else",
        "except",
        "finally",
        "for",
        "from",
        "global",
        "if",
        "import",
        "lambda",
        "match",
        "nonlocal",
        "pass",
        "raise",
        "return",
        "try",
        "while",
        "with",
        "yield");

    /// <summary>Word-style operators classified separately so themes render them with the operator colour rather than the identifier colour.</summary>
    private static readonly ByteKeywordSet WordOperators = ByteKeywordSet.Create(
        "and",
        "in",
        "is",
        "not",
        "or");

    /// <summary>Boolean / null / ellipsis literal set (<c>True</c>, <c>False</c>, <c>None</c>, <c>NotImplemented</c>, <c>Ellipsis</c>).</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        "True",
        "False",
        "None",
        "NotImplemented",
        "Ellipsis");

    /// <summary>Built-in callables and types — trimmed Pygments <c>builtins</c> list covering the names that appear in the rxui corpus.</summary>
    private static readonly ByteKeywordSet Builtins = ByteKeywordSet.Create(
        "abs",
        "all",
        "any",
        "ascii",
        "bin",
        "bool",
        "bytearray",
        "bytes",
        "callable",
        "chr",
        "classmethod",
        "compile",
        "complex",
        "dict",
        "dir",
        "divmod",
        "enumerate",
        "eval",
        "exec",
        "filter",
        "float",
        "format",
        "frozenset",
        "getattr",
        "globals",
        "hasattr",
        "hash",
        "help",
        "hex",
        "id",
        "input",
        "int",
        "isinstance",
        "issubclass",
        "iter",
        "len",
        "list",
        "locals",
        "map",
        "max",
        "memoryview",
        "min",
        "next",
        "object",
        "oct",
        "open",
        "ord",
        "pow",
        "print",
        "property",
        "range",
        "repr",
        "reversed",
        "round",
        "set",
        "setattr",
        "slice",
        "sorted",
        "staticmethod",
        "str",
        "sum",
        "super",
        "tuple",
        "type",
        "vars",
        "zip");

    /// <summary>Operator alternation, sorted longest-first so multi-byte operators win before their single-byte prefixes.</summary>
    private static readonly byte[][] Operators =
    [
        "**="u8.ToArray(), "//="u8.ToArray(), ">>="u8.ToArray(), "<<="u8.ToArray(),
        "**"u8.ToArray(), "//"u8.ToArray(), ">>"u8.ToArray(), "<<"u8.ToArray(),
        "<="u8.ToArray(), ">="u8.ToArray(), "=="u8.ToArray(), "!="u8.ToArray(),
        "+="u8.ToArray(), "-="u8.ToArray(), "*="u8.ToArray(), "/="u8.ToArray(),
        "%="u8.ToArray(), "&="u8.ToArray(), "|="u8.ToArray(), "^="u8.ToArray(),
        "->"u8.ToArray(), ":="u8.ToArray(),
        "+"u8.ToArray(), "-"u8.ToArray(), "*"u8.ToArray(), "/"u8.ToArray(),
        "%"u8.ToArray(), "&"u8.ToArray(), "|"u8.ToArray(), "^"u8.ToArray(),
        "~"u8.ToArray(), "<"u8.ToArray(), ">"u8.ToArray(), "="u8.ToArray(),
    ];

    /// <summary>Hex-digit run including the PEP 515 underscore separator.</summary>
    private static readonly SearchValues<byte> HexBody = SearchValues.Create("0123456789abcdefABCDEF_"u8);

    /// <summary>No-op suffix set — Python integers don't take a typed suffix; supplied so the shared hex / integer matchers compose cleanly.</summary>
    private static readonly SearchValues<byte> EmptySuffix = SearchValues.Create(""u8);

    /// <summary>Float / complex suffix bytes — <c>j</c> / <c>J</c> for the imaginary literal form.</summary>
    private static readonly SearchValues<byte> ComplexSuffix = SearchValues.Create("jJ"u8);

    /// <summary>First-byte set for the boolean / null / ellipsis literals (<c>True</c>, <c>False</c>, <c>None</c>, <c>NotImplemented</c>, <c>Ellipsis</c>).</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("TFNE"u8);

    /// <summary>First-byte set for the language keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefgilmnprtwy"u8);

    /// <summary>First-byte set for the word operators (<c>and</c>, <c>in</c>, <c>is</c>, <c>not</c>, <c>or</c>).</summary>
    private static readonly SearchValues<byte> WordOperatorFirst = SearchValues.Create("ainor"u8);

    /// <summary>First-byte set for operator tokens — every byte that may begin a recognized operator.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%&|^~<>=!:"u8);

    /// <summary>First-byte set for Python structural punctuation.</summary>
    private static readonly SearchValues<byte> Punctuation = SearchValues.Create("(){}[],.;:@"u8);

    /// <summary>First-byte set for hashtag line comments.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>String-prefix bytes — <c>r</c>, <c>R</c>, <c>b</c>, <c>B</c>, <c>u</c>, <c>U</c>, <c>f</c>, <c>F</c>.</summary>
    private static readonly SearchValues<byte> StringPrefix = SearchValues.Create("rRbBuUfF"u8);

    /// <summary>First-byte set for any quoted string — prefix bytes plus bare quote bytes.</summary>
    private static readonly SearchValues<byte> StringFirst = SearchValues.Create("\"'rRbBuUfF"u8);

    /// <summary>Builds the Python root-state rule list.</summary>
    /// <returns>Ordered rule list.</returns>
    public static LexerRule[] Build() =>
        BuildRules();

    /// <summary>Constructs the Python rule list — order matters: longer / more-specific rules precede their substring counterparts.</summary>
    /// <returns>Ordered rule list.</returns>
    private static LexerRule[] BuildRules() =>
    [
        new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiWhitespaceWithNewlines },

        new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },

        new(MatchPrefixedString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = StringFirst },

        new(
            static slice => TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, EmptySuffix),
            TokenClass.NumberHex,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.HexFirst },

        new(static slice => LanguageCommon.MatchFloatWithOptionalSuffix(slice, ComplexSuffix), TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DigitFirst },

        new(
            static slice => TokenMatchers.MatchRunWithSuffix(slice, LanguageCommon.IntegerFirst, ComplexSuffix),
            TokenClass.NumberInteger,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.IntegerFirst },

        new(static slice => TokenMatchers.MatchKeyword(slice, WordOperators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = WordOperatorFirst },

        new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },

        new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },

        new(static slice => TokenMatchers.MatchKeyword(slice, Builtins), TokenClass.NameBuiltin, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

        new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

        new(
            static slice => TokenMatchers.MatchLongestLiteral(slice, Operators),
            TokenClass.Operator,
            LexerRule.NoStateChange) { FirstBytes = OperatorFirst },

        new(
            static slice => TokenMatchers.MatchSingleByteOf(slice, Punctuation),
            TokenClass.Punctuation,
            LexerRule.NoStateChange) { FirstBytes = Punctuation },
    ];

    /// <summary>Matches a Python string literal, including any leading prefix bytes and triple-quoted bodies.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c> on miss.</returns>
    private static int MatchPrefixedString(ReadOnlySpan<byte> slice)
    {
        var prefixLen = ConsumePrefix(slice);
        if (prefixLen >= slice.Length)
        {
            return 0;
        }

        var quote = slice[prefixLen];
        if (quote is not ((byte)'"' or (byte)'\''))
        {
            return 0;
        }

        var rest = slice[prefixLen..];
        var triple = MatchTripleQuotedString(rest, quote);
        if (triple > 0)
        {
            return prefixLen + triple;
        }

        var single = TokenMatchers.MatchQuotedWithBackslashEscape(rest, quote);
        return single is 0 ? 0 : prefixLen + single;
    }

    /// <summary>Consumes up to two ASCII-letter prefix bytes (<c>r</c>, <c>b</c>, <c>u</c>, <c>f</c> in any case / order).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Number of prefix bytes consumed.</returns>
    private static int ConsumePrefix(ReadOnlySpan<byte> slice)
    {
        const int MaxPrefixLength = 2;
        var bound = Math.Min(slice.Length, MaxPrefixLength);
        for (var len = 0; len < bound; len++)
        {
            if (!StringPrefix.Contains(slice[len]))
            {
                return len;
            }
        }

        return bound;
    }

    /// <summary>Matches a triple-quoted string body starting at <paramref name="slice"/>.</summary>
    /// <param name="slice">Slice anchored at the opening quote run (no prefix bytes).</param>
    /// <param name="quote">Quote byte (<c>"</c> or <c>'</c>).</param>
    /// <returns>Length matched (including both delimiter runs), or <c>0</c> on miss.</returns>
    private static int MatchTripleQuotedString(ReadOnlySpan<byte> slice, byte quote)
    {
        if (slice.Length < TripleQuoteOpenAndCloseLength
            || slice[0] != quote || slice[1] != quote || slice[TripleQuoteThirdByteIndex] != quote)
        {
            return 0;
        }

        var rest = slice[TripleQuoteLength..];
        for (var pos = 0; pos < rest.Length;)
        {
            var idx = rest[pos..].IndexOf(quote);
            if (idx < 0)
            {
                return 0;
            }

            pos += idx;
            if (pos + TripleQuoteLength <= rest.Length
                && rest[pos + 1] == quote && rest[pos + TripleQuoteThirdByteIndex] == quote)
            {
                return TripleQuoteLength + pos + TripleQuoteLength;
            }

            pos++;
        }

        return 0;
    }
}
