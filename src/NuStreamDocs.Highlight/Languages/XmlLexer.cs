// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>XML / HTML lexer.</summary>
/// <remarks>
/// Two-state machine: root walks document text and recognizes tag
/// opens / comments / DOCTYPE / CDATA; the tag state handles attribute
/// names, equals, and quoted values before popping back at <c>&gt;</c>
/// or <c>/&gt;</c>.
/// </remarks>
public static class XmlLexer
{
    /// <summary>State id of the tag attribute state.</summary>
    internal const int TagStateId = 1;

    /// <summary>Bytes that terminate a literal-text run in markup mode.</summary>
    private static readonly SearchValues<byte> MarkupTextStop = SearchValues.Create("<&"u8);

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the lexer with both states populated.</summary>
    /// <returns>Configured lexer.</returns>
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1115", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    private static Lexer Build()
    {
        LexerRule[][] states =
        [
            MarkupRootRules.Build(
                TagStateId,

                // Document text — anything up to the next < or &.
                new(static slice => TokenMatchers.MatchRunUntilAny(slice, MarkupTextStop), TokenClass.Text, LexerRule.NoStateChange),

                // [ \t\r\n]+ whitespace runs.
                new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.WhitespaceWithNewlinesFirst },

                // <!-- … --> HTML comment.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<!--"u8, "-->"u8), TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AngleOpenFirst },

                // <![CDATA[ … ]]> CDATA section.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<![CDATA["u8, "]]>"u8), TokenClass.CommentSpecial, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AngleOpenFirst },

                // <!DOCTYPE … > DOCTYPE declaration.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<!DOCTYPE"u8, ">"u8), TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AngleOpenFirst },

                // <? … ?> processing instruction.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<?"u8, "?>"u8), TokenClass.CommentPreproc, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AngleOpenFirst }),
            MarkupTagRules.Build()
        ];
        return new(states);
    }
}
