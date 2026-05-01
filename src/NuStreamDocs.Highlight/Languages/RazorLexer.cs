// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Razor / cshtml lexer.</summary>
/// <remarks>
/// Pragmatic shape: HTML-style root state plus a <c>csharp</c> state
/// that reuses <see cref="CSharpRules.Build"/>. Razor entry tokens
/// (<c>@code</c>, <c>@{</c>, <c>@if</c>, <c>@foreach</c>, …) push the
/// C# state; a closing <c>}</c> pops back to the markup. Inline
/// <c>@identifier</c> expressions stay in markup mode and classify
/// as <see cref="TokenClass.Name"/>; that's enough for the rxui
/// corpus's Razor blocks, which are mostly HTML with a few inline
/// directives.
/// </remarks>
public static class RazorLexer
{
    /// <summary>Name of the embedded C# state in the lexer's state map.</summary>
    private const string CSharpStateName = "csharp";

    /// <summary>Length of a two-character Razor sigil — <c>@@</c>, <c>@{</c>.</summary>
    private const int TwoCharRazorSigilLength = 2;

    /// <summary>Cursor advance after the leading <c>@</c> of an at-keyword or inline expression.</summary>
    private const int AtSigilLength = 1;

    /// <summary>Cursor advance after <c>@</c> + first identifier character — used to skip into the identifier-continuation scan.</summary>
    private const int AfterAtAndFirstIdent = 2;

    /// <summary>Razor block-introducing directives that switch into C# state.</summary>
    private static readonly FrozenSet<string> BlockDirectives = FrozenSet.ToFrozenSet(
        [
            "code", "functions", "inherits", "model", "page", "using", "namespace",
            "attribute", "implements", "inject", "layout", "preservewhitespace",
            "removetag", "section", "service", "typeparam",
        ],
        StringComparer.Ordinal);

    /// <summary>Inline Razor control-flow keywords.</summary>
    private static readonly FrozenSet<string> ControlDirectives = FrozenSet.ToFrozenSet(
        [
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
            "return", "try", "catch", "finally", "lock", "using", "await", "async",
        ],
        StringComparer.Ordinal);

    /// <summary>Continuation set for inline <c>@expr</c> expressions: letters, digits, underscore, dot.</summary>
    private static readonly SearchValues<char> InlineExpressionContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.");

    /// <summary>Bytes that terminate a literal-text run in markup mode.</summary>
    private static readonly SearchValues<char> MarkupTextStop = SearchValues.Create("<&@");

    /// <summary>First-char set for the C# closing brace popper.</summary>
    private static readonly SearchValues<char> CloseBraceFirst = SearchValues.Create("}");

    /// <summary>Gets the singleton lexer instance.</summary>
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1115", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    public static Lexer Instance { get; } = new(
        "razor",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            // Markup state — HTML + Razor entry tokens.
            [Lexer.RootState] = MarkupRootRules.Build(

                // Markup literal-text run — anything up to the next < / & / @.
                new(static slice => TokenMatchers.MatchRunUntilAny(slice, MarkupTextStop), TokenClass.Text, NextState: null),

                // @* … *@ Razor block comment.
                new(static slice => TokenMatchers.MatchDelimited(slice, "@*", "*@"), TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.AtFirst },

                // @@ escaped at-sign — emits as plain text.
                new(static slice => slice is ['@', '@', ..] ? TwoCharRazorSigilLength : 0, TokenClass.Text, NextState: null) { FirstChars = LanguageCommon.AtFirst },

                // @code / @functions / @page / etc. — block directive that pushes the C# state.
                new(static slice => MatchAtKeyword(slice, BlockDirectives), TokenClass.KeywordDeclaration, CSharpStateName) { FirstChars = LanguageCommon.AtFirst },

                // @if / @for / @while / etc. — control directive that pushes the C# state.
                new(static slice => MatchAtKeyword(slice, ControlDirectives), TokenClass.Keyword, CSharpStateName) { FirstChars = LanguageCommon.AtFirst },

                // @{ — brace-block opener that pushes the C# state.
                new(static slice => slice is ['@', '{', ..] ? TwoCharRazorSigilLength : 0, TokenClass.Punctuation, CSharpStateName) { FirstChars = LanguageCommon.AtFirst },

                // @identifier(.identifier)* — inline expression, stays in markup mode.
                new(MatchInlineExpression, TokenClass.Name, NextState: null) { FirstChars = LanguageCommon.AtFirst }),

            ["tag"] = MarkupTagRules.Build(),

            // C# state — reuse the shared rule list and prepend a closing-
            // brace popper that returns to markup.
            [CSharpStateName] = BuildCsharpStateRules(),
        }.ToFrozenDictionary(StringComparer.Ordinal));

    /// <summary>Builds the C# state's rule list — closing-brace popper plus the shared C# rules.</summary>
    /// <returns>The composed rule list.</returns>
    private static LexerRule[] BuildCsharpStateRules()
    {
        var shared = CSharpRules.Build();
        var combined = new LexerRule[shared.Length + 1];
        combined[0] = new(static slice => slice is ['}', ..] ? 1 : 0, TokenClass.Punctuation, LexerRule.StatePop) { FirstChars = CloseBraceFirst };
        Array.Copy(shared, 0, combined, 1, shared.Length);
        return combined;
    }

    /// <summary>Inline expression <c>@identifier(.identifier)*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchInlineExpression(ReadOnlySpan<char> slice)
    {
        if (slice.Length < AfterAtAndFirstIdent
            || slice[0] is not '@'
            || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var rest = slice[AfterAtAndFirstIdent..];
        var stop = rest.IndexOfAnyExcept(InlineExpressionContinue);
        return stop < 0 ? slice.Length : AfterAtAndFirstIdent + stop;
    }

    /// <summary><c>@</c> followed by a keyword from <paramref name="set"/> followed by a non-identifier-continue boundary.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="set">Recognised keyword set.</param>
    /// <returns>Length matched on success, <c>0</c> otherwise.</returns>
    private static int MatchAtKeyword(ReadOnlySpan<char> slice, FrozenSet<string> set)
    {
        if (slice.Length < AfterAtAndFirstIdent
            || slice[0] is not '@'
            || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var bodyStop = slice[AfterAtAndFirstIdent..].IndexOfAnyExcept(TokenMatchers.AsciiIdentifierContinue);
        var end = bodyStop < 0 ? slice.Length : AfterAtAndFirstIdent + bodyStop;
        var word = slice[AtSigilLength..end];
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.Contains(word) ? end : 0;
    }
}
