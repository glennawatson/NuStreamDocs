// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Shared XML-style tag-state rules used by markup lexers.
/// </summary>
internal static class MarkupTagRules
{
    /// <summary>Builds the shared tag-state rule list. Attributes precede tag names so the lookahead-based attribute matcher wins.</summary>
    /// <returns>Rule list.</returns>
    public static LexerRule[] Build() =>
    [

        // [ \t\r\n]+ whitespace runs.
        new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.WhitespaceWithNewlinesFirst },

        // attribute-name with '=' lookahead — must precede the tag-name rule.
        new(LanguageCommon.AttributeName, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.AttributeNameFirst },

        // Tag name (XML name grammar).
        new(LanguageCommon.TagName, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.TagNameFirst },

        // '=' attribute separator.
        new(static slice => TokenMatchers.MatchSingleByteOf(slice, LanguageCommon.EqualsFirst), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.EqualsFirst },

        // "..." double-quoted attribute value (no escapes).
        new(LanguageCommon.DoubleQuotedStringNoEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

        // '...' single-quoted attribute value (no escapes).
        new(TokenMatchers.MatchSingleQuotedNoEscape, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

        // /> self-closing-tag terminator — pops back to root.
        new(LanguageCommon.SelfClose, TokenClass.Punctuation, LexerRule.PopState) { FirstBytes = LanguageCommon.SlashFirst },

        // > tag-close — pops back to root.
        new(static slice => TokenMatchers.MatchSingleByteOf(slice, LanguageCommon.AngleCloseFirst), TokenClass.Punctuation, LexerRule.PopState) { FirstBytes = LanguageCommon.AngleCloseFirst },
    ];
}
