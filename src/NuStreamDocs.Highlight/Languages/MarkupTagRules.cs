// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Shared XML-style tag-state rules used by markup lexers.
/// </summary>
internal static class MarkupTagRules
{
    /// <summary>Builds the shared tag-state rule list, with attributes before tag names so the lookahead-based attribute regex wins.</summary>
    /// <returns>Rule list.</returns>
    public static LexerRule[] Build() =>
    [
        new(LanguageCommon.WhitespaceWithNewlines(), TokenClass.Whitespace, NextState: null) { FirstChars = LanguageCommon.WhitespaceWithNewlinesFirst },
        new(LanguageCommon.AttributeName(), TokenClass.NameAttribute, NextState: null) { FirstChars = LanguageCommon.AttributeNameFirst },
        new(LanguageCommon.TagName(), TokenClass.NameClass, NextState: null) { FirstChars = LanguageCommon.TagNameFirst },
        new(LanguageCommon.EqualsSign(), TokenClass.Operator, NextState: null) { FirstChars = LanguageCommon.EqualsFirst },
        new(LanguageCommon.DoubleQuotedStringNoEscape(), TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst },
        new(LanguageCommon.SingleQuotedStringNoEscape(), TokenClass.StringSingle, NextState: null) { FirstChars = LanguageCommon.SingleQuoteFirst },
        new(LanguageCommon.SelfClose(), TokenClass.Punctuation, LexerRule.StatePop) { FirstChars = LanguageCommon.SlashFirst },
        new(LanguageCommon.AngleClose(), TokenClass.Punctuation, LexerRule.StatePop) { FirstChars = LanguageCommon.AngleCloseFirst },
    ];
}
