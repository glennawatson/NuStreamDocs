// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>PowerShell rule list factory.</summary>
/// <remarks>
/// Pragmatic single-state PowerShell lexer. PowerShell is
/// case-insensitive, so every keyword / operator / verb / alias set
/// uses <see cref="ByteKeywordSet.CreateIgnoreCase"/>.
/// <para>
/// A faithful PowerShell grammar tracks <c>$(...)</c> expression
/// substitutions inside double-quoted strings via a state stack; we
/// collapse to a single-state lexer and treat the entire double-quoted
/// body as one string token. The trade-off is the same one Bash takes
/// for <c>${var}</c> — the surface form still classifies, themes still
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

    /// <summary>Control-flow / declaration keywords (lowercased for case-insensitive lookup).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateIgnoreCase(
        [.. "begin"u8],
        [.. "break"u8],
        [.. "catch"u8],
        [.. "cmdletbinding"u8],
        [.. "continue"u8],
        [.. "data"u8],
        [.. "default"u8],
        [.. "do"u8],
        [.. "dynamicparam"u8],
        [.. "else"u8],
        [.. "elseif"u8],
        [.. "end"u8],
        [.. "exit"u8],
        [.. "filter"u8],
        [.. "finally"u8],
        [.. "for"u8],
        [.. "foreach"u8],
        [.. "from"u8],
        [.. "function"u8],
        [.. "global"u8],
        [.. "if"u8],
        [.. "in"u8],
        [.. "local"u8],
        [.. "param"u8],
        [.. "parameter"u8],
        [.. "process"u8],
        [.. "ref"u8],
        [.. "return"u8],
        [.. "script"u8],
        [.. "switch"u8],
        [.. "throw"u8],
        [.. "trap"u8],
        [.. "try"u8],
        [.. "until"u8],
        [.. "while"u8],
        [.. "validateset"u8],
        [.. "validaterange"u8],
        [.. "validatepattern"u8],
        [.. "validatelength"u8],
        [.. "validatecount"u8],
        [.. "mandatory"u8],
        [.. "parametersetname"u8],
        [.. "position"u8],
        [.. "valuefrompipeline"u8],
        [.. "valuefrompipelinebypropertyname"u8],
        [.. "valuefromremainingarguments"u8],
        [.. "helpmessage"u8]);

    /// <summary>Comparison / logical operator words used after a leading dash (e.g. <c>-eq</c>, <c>-like</c>); lowercased for case-insensitive lookup.</summary>
    private static readonly ByteKeywordSet DashOperators = ByteKeywordSet.CreateIgnoreCase(
        [.. "and"u8],
        [.. "as"u8],
        [.. "band"u8],
        [.. "bnot"u8],
        [.. "bor"u8],
        [.. "bxor"u8],
        [.. "casesensitive"u8],
        [.. "ccontains"u8],
        [.. "ceq"u8],
        [.. "cge"u8],
        [.. "cgt"u8],
        [.. "cle"u8],
        [.. "clike"u8],
        [.. "clt"u8],
        [.. "cmatch"u8],
        [.. "cne"u8],
        [.. "cnotcontains"u8],
        [.. "cnotlike"u8],
        [.. "cnotmatch"u8],
        [.. "contains"u8],
        [.. "creplace"u8],
        [.. "eq"u8],
        [.. "exact"u8],
        [.. "f"u8],
        [.. "file"u8],
        [.. "ge"u8],
        [.. "gt"u8],
        [.. "icontains"u8],
        [.. "ieq"u8],
        [.. "ige"u8],
        [.. "igt"u8],
        [.. "ile"u8],
        [.. "ilike"u8],
        [.. "ilt"u8],
        [.. "imatch"u8],
        [.. "ine"u8],
        [.. "inotcontains"u8],
        [.. "inotlike"u8],
        [.. "inotmatch"u8],
        [.. "ireplace"u8],
        [.. "is"u8],
        [.. "isnot"u8],
        [.. "le"u8],
        [.. "like"u8],
        [.. "lt"u8],
        [.. "match"u8],
        [.. "ne"u8],
        [.. "not"u8],
        [.. "notcontains"u8],
        [.. "notlike"u8],
        [.. "notmatch"u8],
        [.. "or"u8],
        [.. "regex"u8],
        [.. "replace"u8],
        [.. "wildcard"u8]);

    /// <summary>Approved PowerShell verbs (<c>Get-</c>, <c>Set-</c>, <c>New-</c>, …); lowercased for case-insensitive lookup against the verb-noun matcher.</summary>
    private static readonly ByteKeywordSet Verbs = ByteKeywordSet.CreateIgnoreCase(
        [.. "add"u8],
        [.. "approve"u8],
        [.. "assert"u8],
        [.. "backup"u8],
        [.. "block"u8],
        [.. "checkpoint"u8],
        [.. "clear"u8],
        [.. "close"u8],
        [.. "compare"u8],
        [.. "complete"u8],
        [.. "compress"u8],
        [.. "confirm"u8],
        [.. "connect"u8],
        [.. "convert"u8],
        [.. "convertfrom"u8],
        [.. "convertto"u8],
        [.. "copy"u8],
        [.. "debug"u8],
        [.. "deny"u8],
        [.. "disable"u8],
        [.. "disconnect"u8],
        [.. "dismount"u8],
        [.. "edit"u8],
        [.. "enable"u8],
        [.. "enter"u8],
        [.. "exit"u8],
        [.. "expand"u8],
        [.. "export"u8],
        [.. "find"u8],
        [.. "foreach"u8],
        [.. "format"u8],
        [.. "get"u8],
        [.. "grant"u8],
        [.. "group"u8],
        [.. "hide"u8],
        [.. "import"u8],
        [.. "initialize"u8],
        [.. "install"u8],
        [.. "invoke"u8],
        [.. "join"u8],
        [.. "limit"u8],
        [.. "lock"u8],
        [.. "measure"u8],
        [.. "merge"u8],
        [.. "mount"u8],
        [.. "move"u8],
        [.. "new"u8],
        [.. "open"u8],
        [.. "optimize"u8],
        [.. "out"u8],
        [.. "ping"u8],
        [.. "pop"u8],
        [.. "protect"u8],
        [.. "publish"u8],
        [.. "push"u8],
        [.. "read"u8],
        [.. "receive"u8],
        [.. "redo"u8],
        [.. "register"u8],
        [.. "remove"u8],
        [.. "rename"u8],
        [.. "repair"u8],
        [.. "request"u8],
        [.. "reset"u8],
        [.. "resize"u8],
        [.. "resolve"u8],
        [.. "restart"u8],
        [.. "restore"u8],
        [.. "resume"u8],
        [.. "revoke"u8],
        [.. "save"u8],
        [.. "scroll"u8],
        [.. "search"u8],
        [.. "select"u8],
        [.. "send"u8],
        [.. "set"u8],
        [.. "show"u8],
        [.. "skip"u8],
        [.. "sort"u8],
        [.. "split"u8],
        [.. "start"u8],
        [.. "step"u8],
        [.. "stop"u8],
        [.. "submit"u8],
        [.. "suspend"u8],
        [.. "switch"u8],
        [.. "sync"u8],
        [.. "take"u8],
        [.. "tee"u8],
        [.. "test"u8],
        [.. "trace"u8],
        [.. "unblock"u8],
        [.. "undo"u8],
        [.. "uninstall"u8],
        [.. "unlock"u8],
        [.. "unprotect"u8],
        [.. "unpublish"u8],
        [.. "unregister"u8],
        [.. "update"u8],
        [.. "use"u8],
        [.. "wait"u8],
        [.. "watch"u8],
        [.. "where"u8],
        [.. "write"u8]);

    /// <summary>Common PowerShell aliases (<c>cd</c>, <c>ls</c>, <c>gci</c>, …); lowercased for case-insensitive lookup.</summary>
    private static readonly ByteKeywordSet Aliases = ByteKeywordSet.CreateIgnoreCase(
        [.. "ac"u8],
        [.. "asnp"u8],
        [.. "cat"u8],
        [.. "cd"u8],
        [.. "chdir"u8],
        [.. "clc"u8],
        [.. "clear"u8],
        [.. "cli"u8],
        [.. "clp"u8],
        [.. "cls"u8],
        [.. "clv"u8],
        [.. "cnsn"u8],
        [.. "compare"u8],
        [.. "copy"u8],
        [.. "cp"u8],
        [.. "cpi"u8],
        [.. "cpp"u8],
        [.. "curl"u8],
        [.. "cvpa"u8],
        [.. "del"u8],
        [.. "diff"u8],
        [.. "dir"u8],
        [.. "echo"u8],
        [.. "epcsv"u8],
        [.. "epsn"u8],
        [.. "erase"u8],
        [.. "fc"u8],
        [.. "fl"u8],
        [.. "foreach"u8],
        [.. "ft"u8],
        [.. "fw"u8],
        [.. "gal"u8],
        [.. "gc"u8],
        [.. "gci"u8],
        [.. "gcm"u8],
        [.. "gdr"u8],
        [.. "gi"u8],
        [.. "gjb"u8],
        [.. "gl"u8],
        [.. "gm"u8],
        [.. "gmo"u8],
        [.. "gp"u8],
        [.. "gps"u8],
        [.. "group"u8],
        [.. "gsv"u8],
        [.. "gu"u8],
        [.. "gv"u8],
        [.. "gwmi"u8],
        [.. "h"u8],
        [.. "history"u8],
        [.. "icm"u8],
        [.. "iex"u8],
        [.. "ii"u8],
        [.. "ipmo"u8],
        [.. "ipsn"u8],
        [.. "irm"u8],
        [.. "iwmi"u8],
        [.. "iwr"u8],
        [.. "kill"u8],
        [.. "ls"u8],
        [.. "man"u8],
        [.. "md"u8],
        [.. "measure"u8],
        [.. "mi"u8],
        [.. "mount"u8],
        [.. "move"u8],
        [.. "mp"u8],
        [.. "mv"u8],
        [.. "nal"u8],
        [.. "ni"u8],
        [.. "nv"u8],
        [.. "ogv"u8],
        [.. "oh"u8],
        [.. "popd"u8],
        [.. "ps"u8],
        [.. "pushd"u8],
        [.. "pwd"u8],
        [.. "r"u8],
        [.. "rd"u8],
        [.. "rdr"u8],
        [.. "ren"u8],
        [.. "ri"u8],
        [.. "rm"u8],
        [.. "rmdir"u8],
        [.. "rmo"u8],
        [.. "rp"u8],
        [.. "rsn"u8],
        [.. "rv"u8],
        [.. "sajb"u8],
        [.. "sal"u8],
        [.. "saps"u8],
        [.. "sasv"u8],
        [.. "sc"u8],
        [.. "select"u8],
        [.. "set"u8],
        [.. "si"u8],
        [.. "sl"u8],
        [.. "sleep"u8],
        [.. "sls"u8],
        [.. "sort"u8],
        [.. "sp"u8],
        [.. "spps"u8],
        [.. "spsv"u8],
        [.. "start"u8],
        [.. "sv"u8],
        [.. "swmi"u8],
        [.. "tee"u8],
        [.. "type"u8],
        [.. "wget"u8],
        [.. "where"u8],
        [.. "write"u8]);

    /// <summary>Operator alternation, sorted longest-first so multi-byte operators win before their single-byte prefixes.</summary>
    private static readonly byte[][] Operators =
    [
        [.. "&&"u8], [.. "||"u8], [.. "::"u8], [.. ".."u8],
        [.. "++"u8], [.. "--"u8], [.. "+="u8], [.. "-="u8],
        [.. "*="u8], [.. "/="u8], [.. "%="u8],
        [.. "+"u8], [.. "-"u8], [.. "*"u8], [.. "/"u8],
        [.. "%"u8], [.. "&"u8], [.. "|"u8], [.. "!"u8],
        [.. "~"u8], [.. "="u8], [.. "?"u8], [.. "<"u8], [.. ">"u8],
        [.. "^"u8]
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

        new(static slice => TokenMatchers.MatchSingleByteOf(slice, Punctuation), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = Punctuation }
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
            switch (slice[i])
            {
                case (byte)'"':
                    return i + 1;
                case (byte)'`' when i + 1 < slice.Length:
                    {
                        i += BacktickEscapeLength;
                        continue;
                    }

                default:
                    {
                        i++;
                        break;
                    }
            }
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
        _ => 0
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
    /// the rule list stays single-token. The parameter form could emit
    /// <see cref="TokenClass.Name"/>, but the visual difference is
    /// negligible against the operator colour and the rule order shrinks
    /// by one.
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
