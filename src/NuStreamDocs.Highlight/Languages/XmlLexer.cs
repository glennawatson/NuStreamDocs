// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>XML/HTML lexer modelled on Pygments' <c>HtmlLexer</c> shape.</summary>
/// <remarks>
/// Two-state machine: <c>root</c> walks document text and
/// recognises tag opens / comments / DOCTYPE / CDATA;
/// <c>tag</c> handles attribute names, equals, and quoted values
/// before popping back at <c>&gt;</c> or <c>/&gt;</c>.
/// </remarks>
public static class XmlLexer
{
    /// <summary>Bytes that terminate a literal-text run in markup mode.</summary>
    private static readonly SearchValues<char> MarkupTextStop = SearchValues.Create("<&");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the lexer with both states populated.</summary>
    /// <returns>Configured lexer.</returns>
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1114", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1115", Justification = "Each rule is preceded by a blank-line-separated comment so the rule list reads top-to-bottom.")]
    private static Lexer Build()
    {
        var states = new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = MarkupRootRules.Build(

                // Document text — anything up to the next < or &.
                new(static slice => TokenMatchers.MatchRunUntilAny(slice, MarkupTextStop), TokenClass.Text, NextState: null),

                // [ \t\r\n]+ whitespace runs.
                new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, NextState: null) { FirstChars = LanguageCommon.WhitespaceWithNewlinesFirst },

                // <!-- … --> HTML comment.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<!--", "-->"), TokenClass.CommentMulti, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst },

                // <![CDATA[ … ]]> CDATA section.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<![CDATA[", "]]>"), TokenClass.CommentSpecial, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst },

                // <!DOCTYPE … > DOCTYPE declaration.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<!DOCTYPE", ">"), TokenClass.CommentPreproc, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst },

                // <? … ?> processing instruction.
                new(static slice => TokenMatchers.MatchDelimited(slice, "<?", "?>"), TokenClass.CommentPreproc, NextState: null) { FirstChars = LanguageCommon.AngleOpenFirst }),

            ["tag"] = MarkupTagRules.Build(),
        }.ToFrozenDictionary(StringComparer.Ordinal);
        return new("xml", states);
    }
}
