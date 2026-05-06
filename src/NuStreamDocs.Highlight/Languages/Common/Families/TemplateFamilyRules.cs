// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Reusable templating-engine lexer rule builder.</summary>
/// <remarks>
/// Single-state lexer covering the host-language pass-through (text up to the
/// next delimiter opener) plus statement / expression / comment delimiter
/// blocks. Each delimiter block is consumed as a single token rather than
/// re-entered for nested classification — themes still get the visual hook,
/// and the byte cost stays flat. Future per-engine refinement (recursive
/// expression classification) can layer on top.
/// </remarks>
internal static class TemplateFamilyRules
{
    /// <summary>First-byte set for whitespace runs.</summary>
    public static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>Builds a single-state templating <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in TemplateFamilyConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the templating-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in TemplateFamilyConfig config)
    {
        const int MaxRuleSlots = 6;
        var rules = new List<LexerRule>(MaxRuleSlots);

        var stmtOpen = config.StatementOpen;
        var stmtClose = config.StatementClose;
        var exprOpen = config.ExpressionOpen;
        var exprClose = config.ExpressionClose;

        // Comment block, if configured. Match before the statement / expression rules
        // because some engines (Handlebars: `{{!-- ... --}}` overlaps `{{ }}`) share a leading byte.
        if (config.CommentOpen is { } commentOpen && config.CommentClose is { } commentClose)
        {
            var commentFirst = SearchValues.Create(commentOpen.AsSpan(0, 1));
            rules.Add(new(
                slice => TokenMatchers.MatchDelimited(slice, commentOpen, commentClose),
                TokenClass.CommentMulti,
                LexerRule.NoStateChange) { FirstBytes = commentFirst });
        }

        // Statement / expression blocks. Order matters when one opener is a strict prefix
        // of the other (ERB <% / <%=, Handlebars {{ / {{#) — emit the longer-prefixed rule
        // first so the longer literal wins at dispatch.
        var stmtFirst = SearchValues.Create(stmtOpen.AsSpan(0, 1));
        var exprFirst = SearchValues.Create(exprOpen.AsSpan(0, 1));
        var stmtRule = new LexerRule(
            slice => TokenMatchers.MatchDelimited(slice, stmtOpen, stmtClose),
            TokenClass.Keyword,
            LexerRule.NoStateChange) { FirstBytes = stmtFirst };
        var exprRule = new LexerRule(
            slice => TokenMatchers.MatchDelimited(slice, exprOpen, exprClose),
            TokenClass.Name,
            LexerRule.NoStateChange) { FirstBytes = exprFirst };
        if (exprOpen.Length > stmtOpen.Length)
        {
            rules.Add(exprRule);
            rules.Add(stmtRule);
        }
        else
        {
            rules.Add(stmtRule);
            rules.Add(exprRule);
        }

        // Whitespace.
        rules.Add(new(
            TokenMatchers.MatchAsciiWhitespace,
            TokenClass.Whitespace,
            LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst });

        return [.. rules];
    }
}
