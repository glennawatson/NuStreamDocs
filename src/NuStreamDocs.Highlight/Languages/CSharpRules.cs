// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Reusable C# rule list factory, shared between <see cref="CSharpLexer"/>
/// and <see cref="RazorLexer"/>.
/// </summary>
/// <remarks>
/// Extracted as a helper so embedded-language scenarios (Razor's
/// <c>@code { ... }</c> blocks, Markdown-in-HTML, future Blazor
/// components) all classify C# tokens the same way without duplicating
/// the rule list.
/// </remarks>
internal static class CSharpRules
{
    /// <summary>Length of the <c>///</c> doc-comment introducer.</summary>
    private const int DocCommentPrefixLength = 3;

    /// <summary>Length of a basic <c>'x'</c> character literal.</summary>
    private const int BasicCharLiteralLength = 3;

    /// <summary>Length of an escaped <c>'\x'</c> character literal.</summary>
    private const int EscapedCharLiteralLength = 4;

    /// <summary>Minimum opening / closing quote run for a raw string literal (<c>"""</c>).</summary>
    private const int RawStringMinQuotes = 3;

    /// <summary>Length of the <c>u8</c> UTF-8 string suffix.</summary>
    private const int Utf8SuffixLength = 2;

    /// <summary>Declaration-keyword set — type/member-declaration introducers and modifiers.</summary>
    private static readonly ByteKeywordSet DeclarationKeywords = ByteKeywordSet.Create(
        "class",
        "struct",
        "interface",
        "enum",
        "record",
        "delegate",
        "namespace",
        "using",
        "var",
        "let",
        "const",
        "readonly",
        "static",
        "abstract",
        "sealed",
        "virtual",
        "override",
        "partial",
        "public",
        "private",
        "protected",
        "internal",
        "extern",
        "new",
        "this",
        "base",
        "file",
        "required",
        "init",
        "scoped",
        "extension",
        "union");

    /// <summary>General-keyword set — control flow, contextual modifiers, generic constraints.</summary>
    private static readonly ByteKeywordSet GeneralKeywords = ByteKeywordSet.Create(
        "if",
        "else",
        "for",
        "foreach",
        "while",
        "do",
        "return",
        "switch",
        "case",
        "default",
        "break",
        "continue",
        "goto",
        "try",
        "catch",
        "finally",
        "throw",
        "await",
        "async",
        "yield",
        "in",
        "out",
        "ref",
        "params",
        "where",
        "select",
        "from",
        "join",
        "orderby",
        "group",
        "by",
        "into",
        "on",
        "equals",
        "is",
        "as",
        "typeof",
        "sizeof",
        "stackalloc",
        "nameof",
        "when",
        "with",
        "fixed",
        "lock",
        "unsafe",
        "operator",
        "implicit",
        "explicit",
        "checked",
        "unchecked",
        "global",
        "allows",
        "notnull",
        "unmanaged");

    /// <summary>Accessor opener keyword set — <c>get</c>, <c>set</c>, <c>init</c>. Triggers the property-accessor state when followed by <c>{</c> or <c>=&gt;</c>.</summary>
    private static readonly ByteKeywordSet AccessorOpeners = ByteKeywordSet.Create("get", "set", "init");

    /// <summary>Keywords recognized only inside a property accessor body — <c>field</c> (C# 13 backing-field) and <c>value</c> (setter-parameter).</summary>
    private static readonly ByteKeywordSet AccessorContextualKeywords = ByteKeywordSet.Create("field", "value");

    /// <summary>First-byte set for the accessor contextual keywords (<c>field</c> / <c>value</c>).</summary>
    private static readonly SearchValues<byte> AccessorContextualFirst = SearchValues.Create("fv"u8);

    /// <summary>Built-in numeric / object type keyword set.</summary>
    private static readonly ByteKeywordSet TypeKeywords = ByteKeywordSet.Create(
        "bool",
        "byte",
        "sbyte",
        "short",
        "ushort",
        "int",
        "uint",
        "long",
        "ulong",
        "float",
        "double",
        "decimal",
        "char",
        "string",
        "object",
        "void",
        "nint",
        "nuint",
        "dynamic");

    /// <summary>Boolean / null literal set.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create("true", "false", "null");

    /// <summary>Operator alternation, sorted longest-first so a multi-char operator (<c>==</c>, <c>=&gt;</c>) wins before its single-char prefix.</summary>
    private static readonly byte[][] Operators =
    [
        "??="u8.ToArray(), "<<="u8.ToArray(), ">>="u8.ToArray(),
        "=>"u8.ToArray(), "<="u8.ToArray(), ">="u8.ToArray(), "=="u8.ToArray(), "!="u8.ToArray(),
        "&&"u8.ToArray(), "||"u8.ToArray(), "++"u8.ToArray(), "--"u8.ToArray(), "->"u8.ToArray(),
        "<<"u8.ToArray(), ">>"u8.ToArray(), "?."u8.ToArray(), "??"u8.ToArray(), "::"u8.ToArray(),
        "<"u8.ToArray(), ">"u8.ToArray(), "+"u8.ToArray(), "-"u8.ToArray(), "*"u8.ToArray(), "/"u8.ToArray(),
        "%"u8.ToArray(), "&"u8.ToArray(), "|"u8.ToArray(), "^"u8.ToArray(), "!"u8.ToArray(), "~"u8.ToArray(),
        "="u8.ToArray(), "?"u8.ToArray(),
    ];

    /// <summary>Integer / hex suffix bytes.</summary>
    private static readonly SearchValues<byte> IntegerSuffix = SearchValues.Create("uUlL"u8);

    /// <summary>Float suffix bytes.</summary>
    private static readonly SearchValues<byte> FloatSuffix = SearchValues.Create("fFdDmM"u8);

    /// <summary>Hex-digit run continuation: 0-9 a-f A-F plus underscore separator.</summary>
    private static readonly SearchValues<byte> HexBody = SearchValues.Create("0123456789abcdefABCDEF_"u8);

    /// <summary>First-byte set for whitespace runs (C# tokens; newlines are not consumed by this rule).</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = SearchValues.Create(" \t"u8);

    /// <summary>First-byte set for preprocessor directives — leading whitespace plus <c>#</c>.</summary>
    private static readonly SearchValues<byte> PreprocessorFirst = SearchValues.Create(" \t#"u8);

    /// <summary>First-byte set for interpolated-string introducers (<c>$"..."</c>, <c>$$"""..."""</c>).</summary>
    private static readonly SearchValues<byte> DollarFirst = SearchValues.Create("$"u8);

    /// <summary>First-byte set for the boolean / null literals.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for built-in type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("bsuilfdcovn"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("csiernduvlaoptbf"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("iefwdrscbgtayophjnlu"u8);

    /// <summary>First-byte set for operator tokens.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("?=<>!&|+-*/%^~:"u8);

    /// <summary>First-byte set for the accessor opener keywords (<c>get</c> / <c>set</c> / <c>init</c>).</summary>
    private static readonly SearchValues<byte> AccessorOpenerFirst = SearchValues.Create("gsi"u8);

    /// <summary>First-byte set for an opening brace.</summary>
    private static readonly SearchValues<byte> OpenBraceFirst = SearchValues.Create("{"u8);

    /// <summary>First-byte set for a closing brace.</summary>
    private static readonly SearchValues<byte> CloseBraceFirst = SearchValues.Create("}"u8);

    /// <summary>First-byte set for a semicolon.</summary>
    private static readonly SearchValues<byte> SemicolonFirst = SearchValues.Create(";"u8);

    /// <summary>Builds the C# root-state rule list. Pushes into a property-accessor state when it sees <c>get/set/init</c> followed by <c>{</c> or <c>=&gt;</c>.</summary>
    /// <param name="blockAccessorStateId">State id of the block-accessor state to push on a <c>get/set/init {</c> match.</param>
    /// <param name="arrowAccessorStateId">State id of the arrow-accessor state to push on a <c>get/set/init =&gt;</c> match.</param>
    /// <returns>Ordered rule list.</returns>
    public static LexerRule[] Build(int blockAccessorStateId, int arrowAccessorStateId) =>
        BuildRules(includeAccessorContextualKeywords: false, includeAccessorEntry: true, blockAccessorStateId, arrowAccessorStateId);

    /// <summary>Builds the rule list for the block-body accessor state — same as root plus <c>field</c>/<c>value</c> keyword recognition, with <c>{</c> nesting and <c>}</c> popping.</summary>
    /// <param name="blockAccessorStateId">State id of the block-accessor state (used by the inner <c>{</c> push).</param>
    /// <returns>Rule list.</returns>
    public static LexerRule[] BuildBlockAccessorRules(int blockAccessorStateId)
    {
        const int TransitionRuleCount = 2;
        var rules = BuildRules(includeAccessorContextualKeywords: true, includeAccessorEntry: false, blockAccessorStateId: -1, arrowAccessorStateId: -1);
        var withTransitions = new LexerRule[rules.Length + TransitionRuleCount];

        // Push another block-accessor frame on every '{' so nested blocks are tracked.
        withTransitions[0] = new(
            static slice => slice is [(byte)'{', ..] ? 1 : 0,
            TokenClass.Punctuation,
            blockAccessorStateId) { FirstBytes = OpenBraceFirst };

        // Pop on '}'.
        withTransitions[1] = new(
            static slice => slice is [(byte)'}', ..] ? 1 : 0,
            TokenClass.Punctuation,
            LexerRule.PopState) { FirstBytes = CloseBraceFirst };

        Array.Copy(rules, 0, withTransitions, TransitionRuleCount, rules.Length);
        return withTransitions;
    }

    /// <summary>Builds the rule list for the arrow-body accessor state — same as root plus <c>field</c>/<c>value</c> keyword recognition, with <c>;</c> popping.</summary>
    /// <returns>Rule list.</returns>
    public static LexerRule[] BuildArrowAccessorRules()
    {
        var rules = BuildRules(includeAccessorContextualKeywords: true, includeAccessorEntry: false, blockAccessorStateId: -1, arrowAccessorStateId: -1);
        var withTransitions = new LexerRule[rules.Length + 1];

        // Pop on ';' (arrow body terminator).
        withTransitions[0] = new(
            static slice => slice is [(byte)';', ..] ? 1 : 0,
            TokenClass.Punctuation,
            LexerRule.PopState) { FirstBytes = SemicolonFirst };

        Array.Copy(rules, 0, withTransitions, 1, rules.Length);
        return withTransitions;
    }

    /// <summary>Constructs a collection of lexer rules for classifying C# tokens with respect to syntax highlighting.</summary>
    /// <param name="includeAccessorContextualKeywords">Specifies whether contextual keywords related to accessors (e.g., field, value) should be included in the rules.</param>
    /// <param name="includeAccessorEntry">Indicates whether rules for accessor entry points should be included.</param>
    /// <param name="blockAccessorStateId">State id to push when an accessor opens with <c>{</c>.</param>
    /// <param name="arrowAccessorStateId">State id to push when an accessor opens with <c>=&gt;</c>.</param>
    /// <returns>An array of lexer rules for token classification in C# syntax highlighting.</returns>
    private static LexerRule[] BuildRules(bool includeAccessorContextualKeywords, bool includeAccessorEntry, int blockAccessorStateId, int arrowAccessorStateId)
    {
        const int FixedRuleCount = 20;
        var optionalEntries = (includeAccessorEntry ? 2 : 0) + (includeAccessorContextualKeywords ? 1 : 0);
        var rules = new LexerRule[FixedRuleCount + optionalEntries];
        var i = 0;

        // [ \t]+ — non-newline whitespace runs.
        rules[i++] = new(TokenMatchers.MatchAsciiInlineWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst };

        // /// xml-doc-comment to end-of-line — must precede the line-comment rule.
        rules[i++] = new(
            static slice => slice is [(byte)'/', (byte)'/', (byte)'/', ..] ? DocCommentPrefixLength + TokenMatchers.LineLength(slice[DocCommentPrefixLength..]) : 0,
            TokenClass.CommentSpecial,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

        // // line comment to end-of-line.
        rules[i++] = new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

        // /* block comment */ — non-greedy.
        rules[i++] = new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

        // # preprocessor directive — line-anchored, optional leading [ \t].
        rules[i++] = new(MatchPreprocessor, TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = PreprocessorFirst, RequiresLineStart = true };

        // @"..." verbatim string with "" as the embedded-quote escape.
        rules[i++] = new(MatchVerbatimString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AtFirst };

        // $"..." / $$"..." / $"""...""" / $$"""...""" interpolated string (C# 6+ / 11+).
        rules[i++] = new(MatchInterpolatedString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DollarFirst };

        // """...""" raw string (C# 11+) — must precede the regular string rule.
        rules[i++] = new(
            static slice => TokenMatchers.MatchRawQuotedString(slice, (byte)'"', RawStringMinQuotes),
            TokenClass.StringDouble,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst };

        // "..." regular double-quoted string with optional u8 UTF-8 suffix (C# 11+).
        rules[i++] = new(MatchRegularOrUtf8String, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst };

        // 'x' or '\x' single-character literal.
        rules[i++] = new(
            static slice => slice switch
            {
                [(byte)'\'', (byte)'\\', _, (byte)'\'', ..] => EscapedCharLiteralLength,
                [(byte)'\'', _, (byte)'\'', ..] => BasicCharLiteralLength,
                _ => 0,
            },
            TokenClass.StringSingle,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst };

        // 0x[hex_]+[uUlL]* hex integer literal — must precede the integer rule.
        rules[i++] = new(
            static slice => TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, IntegerSuffix),
            TokenClass.NumberHex,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.HexFirst };

        // [0-9]+\.[0-9]+([eE][+-]?[0-9]+)?[fFdDmM]? float literal — must precede the integer rule.
        rules[i++] = new(MatchFloatNumber, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DigitFirst };

        // [0-9_]+[uUlL]* integer literal.
        rules[i++] = new(
            static slice => TokenMatchers.MatchRunWithSuffix(slice, LanguageCommon.IntegerFirst, IntegerSuffix),
            TokenClass.NumberInteger,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.IntegerFirst };

        // true / false / null literal.
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst };

        // Built-in type keyword (bool, int, string, dynamic, ...).
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, TypeKeywords), TokenClass.KeywordType, LexerRule.NoStateChange) { FirstBytes = KeywordTypeFirst };

        // get / set / init followed by '{' — emit keyword, push block-accessor state. Must precede the declaration-keyword rule.
        if (includeAccessorEntry)
        {
            var blockId = blockAccessorStateId;
            rules[i++] = new(
                static slice => MatchAccessorOpener(slice, requireBrace: true),
                TokenClass.KeywordDeclaration,
                blockId) { FirstBytes = AccessorOpenerFirst };

            var arrowId = arrowAccessorStateId;

            // get / set / init followed by '=>' — emit keyword, push arrow-accessor state.
            rules[i++] = new(
                static slice => MatchAccessorOpener(slice, requireBrace: false),
                TokenClass.KeywordDeclaration,
                arrowId) { FirstBytes = AccessorOpenerFirst };
        }

        // Declaration keyword (class, struct, public, init, scoped, extension, union, ...).
        rules[i++] = new(
            static slice => TokenMatchers.MatchKeyword(slice, DeclarationKeywords),
            TokenClass.KeywordDeclaration,
            LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst };

        // field / value — only recognized inside an accessor body.
        if (includeAccessorContextualKeywords)
        {
            rules[i++] = new(
                static slice => TokenMatchers.MatchKeyword(slice, AccessorContextualKeywords),
                TokenClass.Keyword,
                LexerRule.NoStateChange) { FirstBytes = AccessorContextualFirst };
        }

        // General keyword (if, for, await, with, allows, notnull, unmanaged, ...).
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, GeneralKeywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst };

        // [A-Za-z_][A-Za-z0-9_]* identifier — falls through after every keyword set above misses.
        rules[i++] = new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart };

        // Operator alternation (longest-first).
        rules[i++] = new(static slice => TokenMatchers.MatchLongestLiteral(slice, Operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst };

        // Single-byte C-curly punctuation: ( ) { } [ ] ; , . :
        rules[i++] = new(
            static slice => TokenMatchers.MatchSingleByteOf(slice, LanguageCommon.CCurlyPunctuationFirst),
            TokenClass.Punctuation,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.CCurlyPunctuationFirst };

        return rules;
    }

    /// <summary>
    /// Matches an accessor opener keyword ("get", "set", or "init") in the provided slice of bytes.
    /// Optionally checks for a specific symbol (e.g., '{' or '=>') following the keyword depending on the requirement.
    /// </summary>
    /// <param name="slice">The byte span to analyze for a potential accessor opener.</param>
    /// <param name="requireBrace">A boolean flag indicating whether a '{' byte is required immediately following the matched keyword. If set to false, the method checks for '=&gt;' instead.</param>
    /// <returns>The length of the matched keyword if a valid accessor opener is found, or 0 if no match is identified.</returns>
    private static int MatchAccessorOpener(ReadOnlySpan<byte> slice, bool requireBrace)
    {
        var keywordLen = TokenMatchers.MatchKeyword(slice, AccessorOpeners);
        if (keywordLen is 0)
        {
            return 0;
        }

        var ws = TokenMatchers.MatchAsciiWhitespace(slice[keywordLen..]);
        var lookaheadAt = keywordLen + ws;
        if (lookaheadAt >= slice.Length)
        {
            return 0;
        }

        if (requireBrace)
        {
            return slice[lookaheadAt] is (byte)'{' ? keywordLen : 0;
        }

        return lookaheadAt + 1 < slice.Length && slice[lookaheadAt] is (byte)'=' && slice[lookaheadAt + 1] is (byte)'>'
            ? keywordLen
            : 0;
    }

    /// <summary>Preprocessor directive — optional leading <c>[ \t]</c>, then <c>#</c>, then the rest of the line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchPreprocessor(ReadOnlySpan<byte> slice)
    {
        var indent = TokenMatchers.MatchAsciiInlineWhitespace(slice);
        return indent >= slice.Length || slice[indent] is not (byte)'#'
            ? 0
            : indent + 1 + TokenMatchers.LineLength(slice[(indent + 1)..]);
    }

    /// <summary>Verbatim string <c>@"…"</c> with <c>""</c> as the embedded-quote escape.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchVerbatimString(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'@')
        {
            return 0;
        }

        var body = TokenMatchers.MatchDoubleQuotedDoubledEscape(slice[1..]);
        return body is 0 ? 0 : 1 + body;
    }

    /// <summary>Regular double-quoted string with backslash escapes, with an optional <c>u8</c> suffix (C# 11+ UTF-8 string literal).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchRegularOrUtf8String(ReadOnlySpan<byte> slice)
    {
        var stringLen = TokenMatchers.MatchDoubleQuotedWithBackslashEscape(slice);
        if (stringLen is 0)
        {
            return 0;
        }

        var afterQuote = slice[stringLen..];
        return afterQuote is [(byte)'u', (byte)'8', ..] ? stringLen + Utf8SuffixLength : stringLen;
    }

    /// <summary>
    /// Interpolated string literal — <c>$"..."</c>, <c>$$"..."</c>, <c>$"""..."""</c>,
    /// or <c>$$"""..."""</c> (C# 6+ / C# 11+ raw). The body is consumed as a single
    /// token; interpolation expressions are not separately classified.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchInterpolatedString(ReadOnlySpan<byte> slice)
    {
        var dollars = 0;
        while (dollars < slice.Length && slice[dollars] is (byte)'$')
        {
            dollars++;
        }

        if (dollars is 0 || dollars >= slice.Length || slice[dollars] is not (byte)'"')
        {
            return 0;
        }

        // Raw form first — three or more quotes after the $ run.
        var afterDollars = slice[dollars..];
        var rawLen = TokenMatchers.MatchRawQuotedString(afterDollars, (byte)'"', RawStringMinQuotes);
        if (rawLen > 0)
        {
            return dollars + rawLen;
        }

        // Single-quote interpolated form. The standard backslash matcher
        // handles the body — <c>{{</c> / <c>}}</c> are literal braces and
        // don't break the string token at the lexer level.
        var quoted = TokenMatchers.MatchDoubleQuotedWithBackslashEscape(afterDollars);
        return quoted is 0 ? 0 : dollars + quoted;
    }

    /// <summary>Float literal — unsigned float with an optional one-byte C# suffix (<c>f</c> / <c>F</c> / <c>d</c> / <c>D</c> / <c>m</c> / <c>M</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchFloatNumber(ReadOnlySpan<byte> slice)
    {
        var matched = TokenMatchers.MatchUnsignedAsciiFloat(slice);
        if (matched is 0)
        {
            return 0;
        }

        return matched < slice.Length && FloatSuffix.Contains(slice[matched]) ? matched + 1 : matched;
    }
}
