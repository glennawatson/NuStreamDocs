// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>PowerShell rule list factory.</summary>
/// <remarks>
/// Pragmatic single-state port of Pygments' <c>PowerShellLexer</c>
/// (<c>pygments/lexers/shell.py</c>). PowerShell is case-insensitive,
/// so every keyword / operator / verb / alias set uses
/// <see cref="ByteKeywordSet.CreateIgnoreCase"/>.
/// <para>
/// Pygments tracks <c>$(...)</c> expression substitutions inside
/// double-quoted strings via a state stack; we collapse to a
/// single-state lexer and treat the entire double-quoted body as one
/// string token. The trade-off is the same one Bash takes for
/// <c>${var}</c> — the surface form still classifies, themes still
/// colour the literal, and the byte cost stays flat.
/// </para>
/// </remarks>
internal static class PowerShellRules
{
    /// <summary>Length of the <c>&lt;#</c> block-comment opener and <c>#&gt;</c> closer.</summary>
    private const int BlockCommentDelimiterLength = 2;

    /// <summary>Combined length of the block-comment opener plus closer (<c>&lt;# ... #&gt;</c>).</summary>
    private const int BlockCommentMinLength = BlockCommentDelimiterLength * 2;

    /// <summary>Length of the <c>@@</c> double-at variable sigil.</summary>
    private const int DoubleAtSigilLength = 2;

    /// <summary>Length of the bracket pair (<c>[</c> + <c>]</c>) wrapping a PowerShell type reference.</summary>
    private const int BracketPairLength = 2;

    /// <summary>Control-flow / declaration keywords from Pygments' PowerShell <c>keywords</c> list (lowercased for case-insensitive lookup).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateIgnoreCase(
        "begin",
        "break",
        "catch",
        "cmdletbinding",
        "continue",
        "data",
        "default",
        "do",
        "dynamicparam",
        "else",
        "elseif",
        "end",
        "exit",
        "filter",
        "finally",
        "for",
        "foreach",
        "from",
        "function",
        "global",
        "if",
        "in",
        "local",
        "param",
        "parameter",
        "process",
        "ref",
        "return",
        "script",
        "switch",
        "throw",
        "trap",
        "try",
        "until",
        "while",
        "validateset",
        "validaterange",
        "validatepattern",
        "validatelength",
        "validatecount",
        "mandatory",
        "parametersetname",
        "position",
        "valuefrompipeline",
        "valuefrompipelinebypropertyname",
        "valuefromremainingarguments",
        "helpmessage");

    /// <summary>Comparison / logical operator words used after a leading dash (e.g. <c>-eq</c>, <c>-like</c>); lowercased for case-insensitive lookup.</summary>
    private static readonly ByteKeywordSet DashOperators = ByteKeywordSet.CreateIgnoreCase(
        "and",
        "as",
        "band",
        "bnot",
        "bor",
        "bxor",
        "casesensitive",
        "ccontains",
        "ceq",
        "cge",
        "cgt",
        "cle",
        "clike",
        "clt",
        "cmatch",
        "cne",
        "cnotcontains",
        "cnotlike",
        "cnotmatch",
        "contains",
        "creplace",
        "eq",
        "exact",
        "f",
        "file",
        "ge",
        "gt",
        "icontains",
        "ieq",
        "ige",
        "igt",
        "ile",
        "ilike",
        "ilt",
        "imatch",
        "ine",
        "inotcontains",
        "inotlike",
        "inotmatch",
        "ireplace",
        "is",
        "isnot",
        "le",
        "like",
        "lt",
        "match",
        "ne",
        "not",
        "notcontains",
        "notlike",
        "notmatch",
        "or",
        "regex",
        "replace",
        "wildcard");

    /// <summary>Approved PowerShell verbs (<c>Get-</c>, <c>Set-</c>, <c>New-</c>, …); lowercased for case-insensitive lookup against the verb-noun matcher.</summary>
    private static readonly ByteKeywordSet Verbs = ByteKeywordSet.CreateIgnoreCase(
        "add",
        "approve",
        "assert",
        "backup",
        "block",
        "checkpoint",
        "clear",
        "close",
        "compare",
        "complete",
        "compress",
        "confirm",
        "connect",
        "convert",
        "convertfrom",
        "convertto",
        "copy",
        "debug",
        "deny",
        "disable",
        "disconnect",
        "dismount",
        "edit",
        "enable",
        "enter",
        "exit",
        "expand",
        "export",
        "find",
        "foreach",
        "format",
        "get",
        "grant",
        "group",
        "hide",
        "import",
        "initialize",
        "install",
        "invoke",
        "join",
        "limit",
        "lock",
        "measure",
        "merge",
        "mount",
        "move",
        "new",
        "open",
        "optimize",
        "out",
        "ping",
        "pop",
        "protect",
        "publish",
        "push",
        "read",
        "receive",
        "redo",
        "register",
        "remove",
        "rename",
        "repair",
        "request",
        "reset",
        "resize",
        "resolve",
        "restart",
        "restore",
        "resume",
        "revoke",
        "save",
        "scroll",
        "search",
        "select",
        "send",
        "set",
        "show",
        "skip",
        "sort",
        "split",
        "start",
        "step",
        "stop",
        "submit",
        "suspend",
        "switch",
        "sync",
        "take",
        "tee",
        "test",
        "trace",
        "unblock",
        "undo",
        "uninstall",
        "unlock",
        "unprotect",
        "unpublish",
        "unregister",
        "update",
        "use",
        "wait",
        "watch",
        "where",
        "write");

    /// <summary>Common PowerShell aliases (<c>cd</c>, <c>ls</c>, <c>gci</c>, …); lowercased for case-insensitive lookup.</summary>
    private static readonly ByteKeywordSet Aliases = ByteKeywordSet.CreateIgnoreCase(
        "ac",
        "asnp",
        "cat",
        "cd",
        "chdir",
        "clc",
        "clear",
        "cli",
        "clp",
        "cls",
        "clv",
        "cnsn",
        "compare",
        "copy",
        "cp",
        "cpi",
        "cpp",
        "curl",
        "cvpa",
        "del",
        "diff",
        "dir",
        "echo",
        "epcsv",
        "epsn",
        "erase",
        "fc",
        "fl",
        "foreach",
        "ft",
        "fw",
        "gal",
        "gc",
        "gci",
        "gcm",
        "gdr",
        "gi",
        "gjb",
        "gl",
        "gm",
        "gmo",
        "gp",
        "gps",
        "group",
        "gsv",
        "gu",
        "gv",
        "gwmi",
        "h",
        "history",
        "icm",
        "iex",
        "ii",
        "ipmo",
        "ipsn",
        "irm",
        "iwmi",
        "iwr",
        "kill",
        "ls",
        "man",
        "md",
        "measure",
        "mi",
        "mount",
        "move",
        "mp",
        "mv",
        "nal",
        "ni",
        "nv",
        "ogv",
        "oh",
        "popd",
        "ps",
        "pushd",
        "pwd",
        "r",
        "rd",
        "rdr",
        "ren",
        "ri",
        "rm",
        "rmdir",
        "rmo",
        "rp",
        "rsn",
        "rv",
        "sajb",
        "sal",
        "saps",
        "sasv",
        "sc",
        "select",
        "set",
        "si",
        "sl",
        "sleep",
        "sls",
        "sort",
        "sp",
        "spps",
        "spsv",
        "start",
        "sv",
        "swmi",
        "tee",
        "type",
        "wget",
        "where",
        "write");

    /// <summary>Operator alternation, sorted longest-first so multi-byte operators win before their single-byte prefixes.</summary>
    private static readonly byte[][] Operators =
    [
        "&&"u8.ToArray(), "||"u8.ToArray(), "::"u8.ToArray(), ".."u8.ToArray(),
        "++"u8.ToArray(), "--"u8.ToArray(), "+="u8.ToArray(), "-="u8.ToArray(),
        "*="u8.ToArray(), "/="u8.ToArray(), "%="u8.ToArray(),
        "+"u8.ToArray(), "-"u8.ToArray(), "*"u8.ToArray(), "/"u8.ToArray(),
        "%"u8.ToArray(), "&"u8.ToArray(), "|"u8.ToArray(), "!"u8.ToArray(),
        "~"u8.ToArray(), "="u8.ToArray(), "?"u8.ToArray(), "<"u8.ToArray(), ">"u8.ToArray(),
        "^"u8.ToArray(),
    ];

    /// <summary>First-byte set for whitespace runs (newlines included).</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for <c>#</c>-prefixed line comments.</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the block-comment opener (<c>&lt;#</c>).</summary>
    private static readonly SearchValues<byte> AngleOpenFirst = SearchValues.Create("<"u8);

    /// <summary>First-byte set for variable substitutions (<c>$name</c>, <c>${name}</c>, <c>@name</c>, <c>@@name</c>).</summary>
    private static readonly SearchValues<byte> VariableFirst = SearchValues.Create("$@"u8);

    /// <summary>First-byte set for the type-reference shape (<c>[Type]</c>).</summary>
    private static readonly SearchValues<byte> BracketOpenFirst = SearchValues.Create("["u8);

    /// <summary>First-byte set for operator tokens — every byte that may begin a recognized operator.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("&|<>=!+*/%~?^"u8);

    /// <summary>First-byte set for the dash-prefixed operator / parameter shape — handled by a dedicated rule because <c>-</c> is also a binary operator.</summary>
    private static readonly SearchValues<byte> DashFirst = SearchValues.Create("-"u8);

    /// <summary>First-byte set for structural punctuation.</summary>
    private static readonly SearchValues<byte> Punctuation = SearchValues.Create("(){}[],.;:"u8);

    /// <summary>First-byte set for verbs — letters that begin one of the approved-verb words.</summary>
    private static readonly SearchValues<byte> VerbFirst = SearchValues.Create("AaBbCcDdEeFfGgHhIiJjLlMmNnOoPpRrSsTtUuWw"u8);

    /// <summary>First-byte set for any identifier (incl. aliases).</summary>
    private static readonly SearchValues<byte> AliasFirst = TokenMatchers.AsciiIdentifierStart;

    /// <summary>Builds the PowerShell root-state rule list.</summary>
    /// <returns>Ordered rule list.</returns>
    public static LexerRule[] Build() =>
    [
        new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

        new(MatchBlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = AngleOpenFirst },

        new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = CommentFirst },

        new(TokenMatchers.MatchSingleQuotedDoubledEscape, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

        new(MatchDoubleQuotedWithBacktickEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

        new(MatchVariable, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = VariableFirst },

        new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

        new(MatchTypeReference, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = BracketOpenFirst },

        new(MatchDashOperatorOrParameter, TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = DashFirst },

        new(MatchVerbNoun, TokenClass.NameBuiltin, LexerRule.NoStateChange) { FirstBytes = VerbFirst },

        new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

        new(static slice => TokenMatchers.MatchKeyword(slice, Aliases), TokenClass.NameBuiltin, LexerRule.NoStateChange) { FirstBytes = AliasFirst },

        new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

        new(static slice => TokenMatchers.MatchLongestLiteral(slice, Operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },

        new(static slice => TokenMatchers.MatchSingleByteOf(slice, Punctuation), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = Punctuation },
    ];

    /// <summary>PowerShell block comment <c>&lt;# ... #&gt;</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < BlockCommentMinLength
            || slice[0] is not (byte)'<' || slice[1] is not (byte)'#')
        {
            return 0;
        }

        var rest = slice[BlockCommentDelimiterLength..];
        var close = rest.IndexOf("#>"u8);
        return close < 0 ? 0 : BlockCommentDelimiterLength + close + BlockCommentDelimiterLength;
    }

    /// <summary>Double-quoted string with backtick (<c>`</c>) as the escape introducer.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    private static int MatchDoubleQuotedWithBacktickEscape(ReadOnlySpan<byte> slice)
    {
        const int BacktickEscapeLength = 2;
        if (slice is [] || slice[0] is not (byte)'"')
        {
            return 0;
        }

        var i = 1;
        while (i < slice.Length)
        {
            var c = slice[i];
            if (c is (byte)'"')
            {
                return i + 1;
            }

            if (c is (byte)'`' && i + 1 < slice.Length)
            {
                i += BacktickEscapeLength;
                continue;
            }

            i++;
        }

        return 0;
    }

    /// <summary>PowerShell variable: <c>$name</c>, <c>${expr}</c>, <c>@name</c>, <c>@@name</c>, with optional scope (<c>global:</c>, <c>script:</c>, …).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    private static int MatchVariable(ReadOnlySpan<byte> slice)
    {
        var sigilLen = ConsumeSigil(slice);
        if (sigilLen is 0 || sigilLen >= slice.Length)
        {
            return 0;
        }

        if (slice[sigilLen] is (byte)'{')
        {
            var bracket = TokenMatchers.MatchBracketedBlock(slice[sigilLen..], (byte)'{', (byte)'}');
            return bracket is 0 ? 0 : sigilLen + bracket;
        }

        var bodyLen = MatchScopedIdentifier(slice[sigilLen..]);
        return bodyLen is 0 ? 0 : sigilLen + bodyLen;
    }

    /// <summary>Consumes the <c>$</c>, <c>@</c>, or <c>@@</c> variable sigil at the cursor.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Sigil length, or <c>0</c> when the cursor isn't on a sigil byte.</returns>
    private static int ConsumeSigil(ReadOnlySpan<byte> slice) => slice switch
    {
        [(byte)'$', ..] => 1,
        [(byte)'@', (byte)'@', ..] => DoubleAtSigilLength,
        [(byte)'@', ..] => 1,
        _ => 0,
    };

    /// <summary>Reads an identifier with an optional <c>scope:name</c> trailer (<c>global:</c>, <c>script:</c>, <c>private:</c>, <c>env:</c>).</summary>
    /// <param name="slice">Slice anchored after the variable sigil.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    private static int MatchScopedIdentifier(ReadOnlySpan<byte> slice)
    {
        var bodyLen = TokenMatchers.MatchAsciiIdentifier(slice);
        if (bodyLen is 0 || bodyLen >= slice.Length || slice[bodyLen] is not (byte)':')
        {
            return bodyLen;
        }

        var tail = TokenMatchers.MatchAsciiIdentifier(slice[(bodyLen + 1)..]);
        return tail is 0 ? bodyLen : bodyLen + 1 + tail;
    }

    /// <summary>Type reference: <c>[Type]</c>, <c>[System.Int32]</c>; non-greedy bracket pair containing identifier-ish bytes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    private static int MatchTypeReference(ReadOnlySpan<byte> slice)
    {
        const int MinTypeRefLength = 3;
        if (slice.Length < MinTypeRefLength || slice[0] is not (byte)'[')
        {
            return 0;
        }

        // First byte after [ must be a letter / underscore / nested [ — keeps this rule from
        // shadowing the array-indexer punctuation case ([0], [$x]).
        var first = slice[1];
        if (!TokenMatchers.AsciiIdentifierStart.Contains(first) && first is not (byte)'[')
        {
            return 0;
        }

        var close = slice[1..].IndexOf((byte)']');
        return close < 0 ? 0 : close + BracketPairLength;
    }

    /// <summary>Dash-prefixed shape: <c>-EQ</c> / <c>-like</c> as a comparison operator, or <c>-Name</c> as a parameter token.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (dash + identifier), or <c>0</c>.</returns>
    /// <remarks>
    /// Both shapes classify as <see cref="TokenClass.Operator"/> here so
    /// the rule list stays single-token; Pygments emits <c>Name</c> for
    /// the parameter form, but the visual difference is negligible
    /// against the operator colour and the rule order shrinks by one.
    /// </remarks>
    private static int MatchDashOperatorOrParameter(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 2 || slice[0] is not (byte)'-')
        {
            return 0;
        }

        var ident = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        if (ident is 0)
        {
            return 0;
        }

        // Distinguishing operator vs. parameter doesn't change the highlight class here, so
        // both shapes get matched the same way; the keyword-set probe is left in so future
        // refinements (e.g. emitting Name for non-operator dashes) can branch on it.
        var word = slice.Slice(1, ident);
        _ = DashOperators.Contains(word);
        return 1 + ident;
    }

    /// <summary>Verb-noun: an approved verb followed by <c>-</c> and an identifier (<c>Get-Item</c>, <c>New-Object</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    private static int MatchVerbNoun(ReadOnlySpan<byte> slice)
    {
        var verbLen = TokenMatchers.MatchAsciiIdentifier(slice);
        if (verbLen is 0
            || verbLen >= slice.Length
            || slice[verbLen] is not (byte)'-'
            || !Verbs.Contains(slice[..verbLen]))
        {
            return 0;
        }

        var nounLen = TokenMatchers.MatchAsciiIdentifier(slice[(verbLen + 1)..]);
        return nounLen is 0 ? 0 : verbLen + 1 + nounLen;
    }
}
