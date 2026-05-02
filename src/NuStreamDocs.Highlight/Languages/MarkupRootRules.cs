// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Shared XML-style root-state suffix used by markup lexers.</summary>
internal static class MarkupRootRules
{
    /// <summary>Builds a markup root-state rule list by appending the shared tail after <paramref name="leadingRules"/>.</summary>
    /// <param name="tagStateId">State id of the tag state pushed by <c>&lt;</c> / <c>&lt;/</c>.</param>
    /// <param name="textRule">Language-specific text fallback rule.</param>
    /// <param name="leadingRules">Language-specific rules that should run before the common markup tail.</param>
    /// <returns>Combined rule list.</returns>
    public static LexerRule[] Build(int tagStateId, LexerRule textRule, params LexerRule[] leadingRules)
    {
        var output = new LexerRule[leadingRules.Length + 5];
        for (var i = 0; i < leadingRules.Length; i++)
        {
            output[i] = leadingRules[i];
        }

        var index = leadingRules.Length;

        // &name; / &#nnn; entity reference.
        output[index++] = new(LanguageCommon.EntityReference, TokenClass.StringEscape, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.EntityFirst };

        // < tag-open — pushes the tag state.
        output[index++] = new(static slice => TokenMatchers.MatchSingleByteOf(slice, LanguageCommon.AngleOpenFirst), TokenClass.Punctuation, tagStateId) { FirstBytes = LanguageCommon.AngleOpenFirst };

        // </ closing-tag-open — pushes the tag state.
        output[index++] = new(LanguageCommon.AngleOpenSlash, TokenClass.Punctuation, tagStateId) { FirstBytes = LanguageCommon.AngleOpenFirst };

        // Language-specific text-fallback rule (matches everything outside tags + entities).
        output[index++] = textRule;

        // [ \t\r\n]+ whitespace runs — moved to the front by MoveWhitespaceFirst so it wins before the text rule.
        output[index] = new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.WhitespaceWithNewlinesFirst };

        MoveWhitespaceFirst(output);
        return output;
    }

    /// <summary>Moves the shared whitespace rule to the front of the array so it wins before other root-state rules.</summary>
    /// <param name="rules">Combined rule list.</param>
    private static void MoveWhitespaceFirst(LexerRule[] rules)
    {
        var last = rules[^1];
        for (var i = rules.Length - 1; i > 0; i--)
        {
            rules[i] = rules[i - 1];
        }

        rules[0] = last;
    }
}
