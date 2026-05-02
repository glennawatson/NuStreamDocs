// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

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
    /// <summary>State name for an accessor's <c>{...}</c> body. Push on <c>get/set/init</c> + <c>{</c>; <c>{</c> nests, <c>}</c> pops.</summary>
    public const string BlockAccessorState = "block-accessor";

    /// <summary>State name for an accessor's <c>=&gt;</c> arrow body. Push on <c>get/set/init</c> + <c>=&gt;</c>; <c>;</c> pops.</summary>
    public const string ArrowAccessorState = "arrow-accessor";

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
    private static readonly FrozenSet<string> DeclarationKeywords = FrozenSet.ToFrozenSet(
        [
            "class", "struct", "interface", "enum", "record", "delegate", "namespace",
            "using", "var", "let", "const", "readonly", "static", "abstract", "sealed",
            "virtual", "override", "partial", "public", "private", "protected", "internal",
            "extern", "new", "this", "base", "file", "required",
            "init", "scoped", "extension", "union",
        ],
        StringComparer.Ordinal);

    /// <summary>General-keyword set — control flow, contextual modifiers, generic constraints.</summary>
    private static readonly FrozenSet<string> GeneralKeywords = FrozenSet.ToFrozenSet(
        [
            "if", "else", "for", "foreach", "while", "do", "return", "switch", "case",
            "default", "break", "continue", "goto", "try", "catch", "finally", "throw",
            "await", "async", "yield", "in", "out", "ref", "params", "where", "select",
            "from", "join", "orderby", "group", "by", "into", "on", "equals", "is", "as",
            "typeof", "sizeof", "stackalloc", "nameof", "when", "with", "fixed", "lock",
            "unsafe", "operator", "implicit", "explicit", "checked", "unchecked", "global",
            "allows", "notnull", "unmanaged",
        ],
        StringComparer.Ordinal);

    /// <summary>Accessor opener keyword set — <c>get</c>, <c>set</c>, <c>init</c>. Triggers the property-accessor state when followed by <c>{</c> or <c>=&gt;</c>.</summary>
    private static readonly FrozenSet<string> AccessorOpeners = FrozenSet.ToFrozenSet(
        ["get", "set", "init"],
        StringComparer.Ordinal);

    /// <summary>Keywords recognised only inside a property accessor body — <c>field</c> (C# 13 backing-field) and <c>value</c> (setter-parameter).</summary>
    private static readonly FrozenSet<string> AccessorContextualKeywords = FrozenSet.ToFrozenSet(
        ["field", "value"],
        StringComparer.Ordinal);

    /// <summary>First-char set for the accessor contextual keywords (<c>field</c> / <c>value</c>).</summary>
    private static readonly SearchValues<char> AccessorContextualFirst = SearchValues.Create("fv");

    /// <summary>Built-in numeric / object type keyword set.</summary>
    private static readonly FrozenSet<string> TypeKeywords = FrozenSet.ToFrozenSet(
        [
            "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong",
            "float", "double", "decimal", "char", "string", "object", "void", "nint", "nuint",
            "dynamic",
        ],
        StringComparer.Ordinal);

    /// <summary>Boolean / null literal set.</summary>
    private static readonly FrozenSet<string> KeywordConstants = FrozenSet.ToFrozenSet(
        ["true", "false", "null"],
        StringComparer.Ordinal);

    /// <summary>Operator alternation, sorted longest-first so a multi-char operator (<c>==</c>, <c>=&gt;</c>) wins before its single-char prefix.</summary>
    private static readonly string[] Operators =
    [
        "??=", "<<=", ">>=", "=>", "<=", ">=", "==", "!=", "&&", "||", "++", "--", "->",
        "<<", ">>", "?.", "??", "::",
        "<", ">", "+", "-", "*", "/", "%", "&", "|", "^", "!", "~", "=", "?",
    ];

    /// <summary>Integer / hex suffix characters.</summary>
    private static readonly SearchValues<char> IntegerSuffix = SearchValues.Create("uUlL");

    /// <summary>Float suffix characters.</summary>
    private static readonly SearchValues<char> FloatSuffix = SearchValues.Create("fFdDmM");

    /// <summary>Hex-digit run continuation: 0-9 a-f A-F plus underscore separator.</summary>
    private static readonly SearchValues<char> HexBody = SearchValues.Create("0123456789abcdefABCDEF_");

    /// <summary>First-char set for whitespace runs (C# tokens; newlines are not consumed by this rule).</summary>
    private static readonly SearchValues<char> WhitespaceFirst = SearchValues.Create(" \t");

    /// <summary>First-char set for preprocessor directives — leading whitespace plus <c>#</c>.</summary>
    private static readonly SearchValues<char> PreprocessorFirst = SearchValues.Create(" \t#");

    /// <summary>First-char set for interpolated-string introducers (<c>$"..."</c>, <c>$$"""..."""</c>).</summary>
    private static readonly SearchValues<char> DollarFirst = SearchValues.Create("$");

    /// <summary>First-char set for the boolean / null literals.</summary>
    private static readonly SearchValues<char> KeywordConstantFirst = SearchValues.Create("tfn");

    /// <summary>First-char set for built-in type keywords.</summary>
    private static readonly SearchValues<char> KeywordTypeFirst = SearchValues.Create("bsuilfdcovn");

    /// <summary>First-char set for declaration keywords.</summary>
    private static readonly SearchValues<char> KeywordDeclarationFirst = SearchValues.Create("csiernduvlaoptbf");

    /// <summary>First-char set for general keywords.</summary>
    private static readonly SearchValues<char> KeywordFirst = SearchValues.Create("iefwdrscbgtayophjnlu");

    /// <summary>First-char set for operator tokens.</summary>
    private static readonly SearchValues<char> OperatorFirst = SearchValues.Create("?=<>!&|+-*/%^~:");

    /// <summary>First-char set for the accessor opener keywords (<c>get</c> / <c>set</c> / <c>init</c>).</summary>
    private static readonly SearchValues<char> AccessorOpenerFirst = SearchValues.Create("gsi");

    /// <summary>First-char set for an opening brace.</summary>
    private static readonly SearchValues<char> OpenBraceFirst = SearchValues.Create("{");

    /// <summary>First-char set for a closing brace.</summary>
    private static readonly SearchValues<char> CloseBraceFirst = SearchValues.Create("}");

    /// <summary>First-char set for a semicolon.</summary>
    private static readonly SearchValues<char> SemicolonFirst = SearchValues.Create(";");

    /// <summary>Builds the C# rule list. Order matters — doc comments must precede line comments; raw / interpolated string forms must precede the regular string form.</summary>
    /// <returns>Ordered rule list classifying C# tokens with Pygments-shape CSS classes.</returns>
    public static LexerRule[] Build() => BuildRules(includeAccessorContextualKeywords: false, includeAccessorEntry: true);

    /// <summary>Builds the rule list for the block-body accessor state — same as root plus <c>field</c>/<c>value</c> keyword recognition, with <c>{</c> nesting and <c>}</c> popping.</summary>
    /// <returns>Rule list.</returns>
    public static LexerRule[] BuildBlockAccessorRules()
    {
        const int TransitionRuleCount = 2;
        var rules = BuildRules(includeAccessorContextualKeywords: true, includeAccessorEntry: false);
        var withTransitions = new LexerRule[rules.Length + 2];

        // Push another block-accessor frame on every '{' so nested blocks are tracked.
        withTransitions[0] = new(
            static slice => slice is ['{', ..] ? 1 : 0,
            TokenClass.Punctuation,
            BlockAccessorState) { FirstChars = OpenBraceFirst };

        // Pop on '}'.
        withTransitions[1] = new(
            static slice => slice is ['}', ..] ? 1 : 0,
            TokenClass.Punctuation,
            LexerRule.StatePop) { FirstChars = CloseBraceFirst };

        Array.Copy(rules, 0, withTransitions, TransitionRuleCount, rules.Length);
        return withTransitions;
    }

    /// <summary>Builds the rule list for the arrow-body accessor state — same as root plus <c>field</c>/<c>value</c> keyword recognition, with <c>;</c> popping.</summary>
    /// <returns>Rule list.</returns>
    public static LexerRule[] BuildArrowAccessorRules()
    {
        var rules = BuildRules(includeAccessorContextualKeywords: true, includeAccessorEntry: false);
        var withTransitions = new LexerRule[rules.Length + 1];

        // Pop on ';' (arrow body terminator).
        withTransitions[0] = new(
            static slice => slice is [';', ..] ? 1 : 0,
            TokenClass.Punctuation,
            LexerRule.StatePop) { FirstChars = SemicolonFirst };

        Array.Copy(rules, 0, withTransitions, 1, rules.Length);
        return withTransitions;
    }

    /// <summary>Constructs a collection of lexer rules for classifying C# tokens with respect to syntax highlighting.</summary>
    /// <param name="includeAccessorContextualKeywords">Specifies whether contextual keywords related to accessors (e.g., get, set) should be included in the rules.</param>
    /// <param name="includeAccessorEntry">Indicates whether rules for accessor entry points should be included.</param>
    /// <returns>An array of lexer rules for token classification in C# syntax highlighting.</returns>
    private static LexerRule[] BuildRules(bool includeAccessorContextualKeywords, bool includeAccessorEntry)
    {
        const int FixedRuleCount = 20;
        var optionalEntries = (includeAccessorEntry ? 2 : 0) + (includeAccessorContextualKeywords ? 1 : 0);
        var rules = new LexerRule[FixedRuleCount + optionalEntries];
        var i = 0;

        // [ \t]+ — non-newline whitespace runs.
        rules[i++] = new(TokenMatchers.MatchAsciiInlineWhitespace, TokenClass.Whitespace, NextState: null) { FirstChars = WhitespaceFirst };

        // /// xml-doc-comment to end-of-line — must precede the line-comment rule.
        rules[i++] = new(
            static slice => slice is ['/', '/', '/', ..] ? DocCommentPrefixLength + TokenMatchers.LineLength(slice[DocCommentPrefixLength..]) : 0,
            TokenClass.CommentSpecial,
            NextState: null) { FirstChars = LanguageCommon.SlashFirst };

        // // line comment to end-of-line.
        rules[i++] = new(LanguageCommon.LineComment, TokenClass.CommentSingle, NextState: null) { FirstChars = LanguageCommon.SlashFirst };

        // /* block comment */ — non-greedy.
        rules[i++] = new(LanguageCommon.BlockComment, TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.SlashFirst };

        // # preprocessor directive — line-anchored, optional leading [ \t].
        rules[i++] = new(MatchPreprocessor, TokenClass.CommentPreproc, NextState: null) { FirstChars = PreprocessorFirst, RequiresLineStart = true };

        // @"..." verbatim string with "" as the embedded-quote escape.
        rules[i++] = new(MatchVerbatimString, TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.AtFirst };

        // $"..." / $$"..." / $"""...""" / $$"""...""" interpolated string (C# 6+ / 11+).
        rules[i++] = new(MatchInterpolatedString, TokenClass.StringDouble, NextState: null) { FirstChars = DollarFirst };

        // """...""" raw string (C# 11+) — must precede the regular string rule.
        rules[i++] = new(
            static slice => TokenMatchers.MatchRawQuotedString(slice, '"', RawStringMinQuotes),
            TokenClass.StringDouble,
            NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst };

        // "..." regular double-quoted string with optional u8 UTF-8 suffix (C# 11+).
        rules[i++] = new(MatchRegularOrUtf8String, TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst };

        // 'x' or '\x' single-character literal.
        rules[i++] = new(
            static slice => slice switch
            {
                ['\'', '\\', _, '\'', ..] => EscapedCharLiteralLength,
                ['\'', _, '\'', ..] => BasicCharLiteralLength,
                _ => 0,
            },
            TokenClass.StringSingle,
            NextState: null) { FirstChars = LanguageCommon.SingleQuoteFirst };

        // 0x[hex_]+[uUlL]* hex integer literal — must precede the integer rule.
        rules[i++] = new(
            static slice => TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, IntegerSuffix),
            TokenClass.NumberHex,
            NextState: null) { FirstChars = LanguageCommon.HexFirst };

        // [0-9]+\.[0-9]+([eE][+-]?[0-9]+)?[fFdDmM]? float literal — must precede the integer rule.
        rules[i++] = new(MatchFloatNumber, TokenClass.NumberFloat, NextState: null) { FirstChars = LanguageCommon.DigitFirst };

        // [0-9_]+[uUlL]* integer literal.
        rules[i++] = new(
            static slice => TokenMatchers.MatchRunWithSuffix(slice, LanguageCommon.IntegerFirst, IntegerSuffix),
            TokenClass.NumberInteger,
            NextState: null) { FirstChars = LanguageCommon.IntegerFirst };

        // true / false / null literal.
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, NextState: null) { FirstChars = KeywordConstantFirst };

        // Built-in type keyword (bool, int, string, dynamic, ...).
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, TypeKeywords), TokenClass.KeywordType, NextState: null) { FirstChars = KeywordTypeFirst };

        // get / set / init followed by '{' — emit keyword, push block-accessor state. Must precede the declaration-keyword rule.
        if (includeAccessorEntry)
        {
            rules[i++] = new(
                static slice => MatchAccessorOpener(slice, requireBrace: true),
                TokenClass.KeywordDeclaration,
                BlockAccessorState) { FirstChars = AccessorOpenerFirst };

            // get / set / init followed by '=>' — emit keyword, push arrow-accessor state.
            rules[i++] = new(
                static slice => MatchAccessorOpener(slice, requireBrace: false),
                TokenClass.KeywordDeclaration,
                ArrowAccessorState) { FirstChars = AccessorOpenerFirst };
        }

        // Declaration keyword (class, struct, public, init, scoped, extension, union, ...).
        rules[i++] = new(
            static slice => TokenMatchers.MatchKeyword(slice, DeclarationKeywords),
            TokenClass.KeywordDeclaration,
            NextState: null) { FirstChars = KeywordDeclarationFirst };

        // field / value — only recognised inside an accessor body.
        if (includeAccessorContextualKeywords)
        {
            rules[i++] = new(
                static slice => TokenMatchers.MatchKeyword(slice, AccessorContextualKeywords),
                TokenClass.Keyword,
                NextState: null) { FirstChars = AccessorContextualFirst };
        }

        // General keyword (if, for, await, with, allows, notnull, unmanaged, ...).
        rules[i++] = new(static slice => TokenMatchers.MatchKeyword(slice, GeneralKeywords), TokenClass.Keyword, NextState: null) { FirstChars = KeywordFirst };

        // [A-Za-z_][A-Za-z0-9_]* identifier — falls through after every keyword set above misses.
        rules[i++] = new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, NextState: null) { FirstChars = TokenMatchers.AsciiIdentifierStart };

        // Operator alternation (longest-first).
        rules[i++] = new(static slice => TokenMatchers.MatchLongestLiteral(slice, Operators), TokenClass.Operator, NextState: null) { FirstChars = OperatorFirst };

        // Single-character C-curly punctuation: ( ) { } [ ] ; , . :
        rules[i++] = new(
            static slice => TokenMatchers.MatchSingleCharOf(slice, LanguageCommon.CCurlyPunctuationFirst),
            TokenClass.Punctuation,
            NextState: null) { FirstChars = LanguageCommon.CCurlyPunctuationFirst };

        return rules;
    }

    /// <summary>
    /// Matches an accessor opener keyword ("get", "set", or "init") in the provided slice of characters.
    /// Optionally checks for a specific symbol (e.g., '{' or '=>') following the keyword depending on the requirement.
    /// </summary>
    /// <param name="slice">The read-only span of characters to analyze for a potential accessor opener.</param>
    /// <param name="requireBrace">
    /// A boolean flag indicating whether a '{' character is required immediately following the matched keyword.
    /// If set to false, the method checks for '=>' instead.
    /// </param>
    /// <returns>
    /// The length of the matched keyword if a valid accessor opener is found, or 0 if no match is identified.
    /// </returns>
    private static int MatchAccessorOpener(ReadOnlySpan<char> slice, bool requireBrace)
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
            return slice[lookaheadAt] is '{' ? keywordLen : 0;
        }

        return lookaheadAt + 1 < slice.Length && slice[lookaheadAt] is '=' && slice[lookaheadAt + 1] is '>'
            ? keywordLen
            : 0;
    }

    /// <summary>Preprocessor directive — optional leading <c>[ \t]</c>, then <c>#</c>, then the rest of the line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchPreprocessor(ReadOnlySpan<char> slice)
    {
        var indent = TokenMatchers.MatchAsciiInlineWhitespace(slice);
        return indent >= slice.Length || slice[indent] is not '#'
            ? 0
            : indent + 1 + TokenMatchers.LineLength(slice[(indent + 1)..]);
    }

    /// <summary>Verbatim string <c>@"…"</c> with <c>""</c> as the embedded-quote escape.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchVerbatimString(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not '@')
        {
            return 0;
        }

        var body = TokenMatchers.MatchDoubleQuotedDoubledEscape(slice[1..]);
        return body is 0 ? 0 : 1 + body;
    }

    /// <summary>Regular double-quoted string with backslash escapes, with an optional <c>u8</c> suffix (C# 11+ UTF-8 string literal).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchRegularOrUtf8String(ReadOnlySpan<char> slice)
    {
        var stringLen = TokenMatchers.MatchDoubleQuotedWithBackslashEscape(slice);
        if (stringLen is 0)
        {
            return 0;
        }

        var afterQuote = slice[stringLen..];
        return afterQuote is ['u', '8', ..] ? stringLen + Utf8SuffixLength : stringLen;
    }

    /// <summary>
    /// Interpolated string literal — <c>$"..."</c>, <c>$$"..."</c>, <c>$"""..."""</c>,
    /// or <c>$$"""..."""</c> (C# 6+ / C# 11+ raw). The body is consumed as a single
    /// token; interpolation expressions are not separately classified.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchInterpolatedString(ReadOnlySpan<char> slice)
    {
        var dollars = 0;
        while (dollars < slice.Length && slice[dollars] is '$')
        {
            dollars++;
        }

        if (dollars is 0 || dollars >= slice.Length || slice[dollars] is not '"')
        {
            return 0;
        }

        // Raw form first — three or more quotes after the $ run.
        var afterDollars = slice[dollars..];
        var rawLen = TokenMatchers.MatchRawQuotedString(afterDollars, '"', RawStringMinQuotes);
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

    /// <summary>Float literal — unsigned float with an optional one-character C# suffix (<c>f</c> / <c>F</c> / <c>d</c> / <c>D</c> / <c>m</c> / <c>M</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchFloatNumber(ReadOnlySpan<char> slice)
    {
        var matched = TokenMatchers.MatchUnsignedAsciiFloat(slice);
        if (matched is 0)
        {
            return 0;
        }

        return matched < slice.Length && FloatSuffix.Contains(slice[matched]) ? matched + 1 : matched;
    }
}
