// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Highlight.Languages.CFamily;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Markup;

namespace NuStreamDocs.Highlight.Languages.Markup;

/// <summary>Razor / cshtml lexer.</summary>
/// <remarks>
/// Pragmatic shape: HTML-style root state plus a C# state that reuses
/// <see cref="CSharpRules.Build"/>. Razor entry tokens
/// (<c>@code</c>, <c>@{</c>, <c>@if</c>, <c>@foreach</c>, …) push the
/// C# state; a closing <c>}</c> pops back to the markup. Inline
/// <c>@identifier</c> expressions stay in markup mode and classify as
/// <see cref="TokenClass.Name"/>.
/// </remarks>
public static class RazorLexer
{
    /// <summary>State id of the markup tag-attribute state.</summary>
    internal const int TagStateId = 1;

    /// <summary>State id of the embedded C# state.</summary>
    internal const int CSharpStateId = 2;

    /// <summary>State id of an embedded C# accessor's block body (nested inside <see cref="CSharpStateId"/>).</summary>
    internal const int CSharpBlockAccessorStateId = 3;

    /// <summary>State id of an embedded C# accessor's arrow body (nested inside <see cref="CSharpStateId"/>).</summary>
    internal const int CSharpArrowAccessorStateId = 4;

    /// <summary>Length of a two-byte Razor sigil — <c>@@</c>, <c>@{</c>.</summary>
    private const int TwoCharRazorSigilLength = 2;

    /// <summary>Cursor advance after the leading <c>@</c> of an at-keyword or inline expression.</summary>
    private const int AtSigilLength = 1;

    /// <summary>Cursor advance after <c>@</c> + first identifier byte — used to skip into the identifier-continuation scan.</summary>
    private const int AfterAtAndFirstIdent = 2;

    /// <summary>Razor block-introducing directives that switch into C# state.</summary>
    private static readonly ByteKeywordSet BlockDirectives = ByteKeywordSet.Create(
        [.. "code"u8],
        [.. "functions"u8],
        [.. "inherits"u8],
        [.. "model"u8],
        [.. "page"u8],
        [.. "using"u8],
        [.. "namespace"u8],
        [.. "attribute"u8],
        [.. "implements"u8],
        [.. "inject"u8],
        [.. "layout"u8],
        [.. "preservewhitespace"u8],
        [.. "removetag"u8],
        [.. "section"u8],
        [.. "service"u8],
        [.. "typeparam"u8]);

    /// <summary>Inline Razor control-flow keywords.</summary>
    private static readonly ByteKeywordSet ControlDirectives = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "else"u8],
        [.. "for"u8],
        [.. "foreach"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "switch"u8],
        [.. "case"u8],
        [.. "default"u8],
        [.. "return"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "lock"u8],
        [.. "using"u8],
        [.. "await"u8],
        [.. "async"u8]);

    /// <summary>Continuation set for inline <c>@expr</c> expressions: letters, digits, underscore, dot.</summary>
    private static readonly SearchValues<byte> InlineExpressionContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_."u8);

    /// <summary>Bytes that terminate a literal-text run in markup mode.</summary>
    private static readonly SearchValues<byte> MarkupTextStop = SearchValues.Create("<&@"u8);

    /// <summary>First-byte set for the C# closing brace popper.</summary>
    private static readonly SearchValues<byte> CloseBraceFirst = SearchValues.Create("}"u8);

    /// <summary>Gets the singleton lexer instance.</summary>
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1115", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Razor lexer with all five states populated.</summary>
    /// <returns>Configured lexer.</returns>
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114", Justification = "Rule lists read top-to-bottom with comments.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1115", Justification = "Rule lists read top-to-bottom with comments.")]
    private static Lexer Build()
    {
        LexerRule[][] states =
        [
            MarkupRootRules.Build(
                TagStateId,

                // Markup literal-text run — anything up to the next < / & / @.
                new(static slice => TokenMatchers.MatchRunUntilAny(slice, MarkupTextStop), TokenClass.Text, LexerRule.NoStateChange),

                // @* … *@ Razor block comment.
                new(static slice => TokenMatchers.MatchDelimited(slice, "@*"u8, "*@"u8), TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AtFirst },

                // @@ escaped at-sign — emits as plain text.
                new(static slice => slice is [(byte)'@', (byte)'@', ..] ? TwoCharRazorSigilLength : 0, TokenClass.Text, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AtFirst },

                // @code / @functions / @page / etc. — block directive that pushes the C# state.
                new(static slice => MatchAtKeyword(slice, BlockDirectives), TokenClass.KeywordDeclaration, CSharpStateId) { FirstBytes = LanguageCommon.AtFirst },

                // @if / @for / @while / etc. — control directive that pushes the C# state.
                new(static slice => MatchAtKeyword(slice, ControlDirectives), TokenClass.Keyword, CSharpStateId) { FirstBytes = LanguageCommon.AtFirst },

                // @{ — brace-block opener that pushes the C# state.
                new(static slice => slice is [(byte)'@', (byte)'{', ..] ? TwoCharRazorSigilLength : 0, TokenClass.Punctuation, CSharpStateId) { FirstBytes = LanguageCommon.AtFirst },

                // @identifier(.identifier)* — inline expression, stays in markup mode.
                new(MatchInlineExpression, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AtFirst }),
            MarkupTagRules.Build(),
            BuildCsharpStateRules(),
            CSharpRules.BuildBlockAccessorRules(CSharpBlockAccessorStateId),
            CSharpRules.BuildArrowAccessorRules()
        ];
        return new(states);
    }

    /// <summary>Builds the C# state's rule list — closing-brace popper plus the shared C# rules.</summary>
    /// <returns>The composed rule list.</returns>
    private static LexerRule[] BuildCsharpStateRules()
    {
        var shared = CSharpRules.Build(CSharpBlockAccessorStateId, CSharpArrowAccessorStateId);
        var combined = new LexerRule[shared.Length + 1];
        combined[0] = new(static slice => slice is [(byte)'}', ..] ? 1 : 0, TokenClass.Punctuation, LexerRule.PopState) { FirstBytes = CloseBraceFirst };
        Array.Copy(shared, 0, combined, 1, shared.Length);
        return combined;
    }

    /// <summary>Inline expression <c>@identifier(.identifier)*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchInlineExpression(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < AfterAtAndFirstIdent
            || slice[0] is not (byte)'@'
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
    /// <param name="set">Recognized keyword set.</param>
    /// <returns>Length matched on success, <c>0</c> otherwise.</returns>
    private static int MatchAtKeyword(ReadOnlySpan<byte> slice, ByteKeywordSet set)
    {
        if (slice.Length < AfterAtAndFirstIdent
            || slice[0] is not (byte)'@'
            || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var bodyStop = slice[AfterAtAndFirstIdent..].IndexOfAnyExcept(TokenMatchers.AsciiIdentifierContinue);
        var end = bodyStop < 0 ? slice.Length : AfterAtAndFirstIdent + bodyStop;
        var word = slice[AtSigilLength..end];
        return set.Contains(word) ? end : 0;
    }
}
