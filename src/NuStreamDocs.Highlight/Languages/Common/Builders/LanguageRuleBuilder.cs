// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Common.Builders;

/// <summary>Helpers for composing lexer rule arrays and per-state rule tables.</summary>
internal static class LanguageRuleBuilder
{
    /// <summary>Builds the ordered rule list shared by the C# / TypeScript family.</summary>
    /// <param name="rules">Language-specific matcher / classification set.</param>
    /// <returns>Ordered rule list.</returns>
    internal static LexerRule[] BuildCStyleRules(in CStyleRuleSet rules)
    {
        var output = new LexerRule[CountCStyleRules(in rules)];
        var index = 0;
        output[index] = rules.Whitespace;
        index++;
        if (rules.DocComment is { } docComment)
        {
            output[index] = docComment;
            index++;
        }

        output[index] = rules.LineComment;
        index++;
        output[index] = rules.BlockComment;
        index++;
        if (rules.Preprocessor is { } preprocessor)
        {
            output[index] = preprocessor;
            index++;
        }

        if (rules.SpecialString is { } specialString)
        {
            output[index] = specialString;
            index++;
        }

        output[index] = rules.DoubleString;
        index++;
        output[index] = rules.SingleString;
        index++;
        if (rules.CharacterLiteral is { } characterLiteral)
        {
            output[index] = characterLiteral;
            index++;
        }

        output[index] = rules.HexNumber;
        index++;
        output[index] = rules.FloatNumber;
        index++;
        output[index] = rules.IntegerNumber;
        index++;
        output[index] = rules.KeywordConstant;
        index++;
        output[index] = rules.KeywordType;
        index++;
        output[index] = rules.KeywordDeclaration;
        index++;
        output[index] = rules.Keyword;
        index++;
        output[index] = rules.Identifier;
        index++;
        output[index] = rules.Operator;
        index++;
        output[index] = rules.Punctuation;
        return output;
    }

    /// <summary>Builds a state table for a single-state lexer.</summary>
    /// <param name="rules">Root-state rules.</param>
    /// <returns>State table indexed by state id; <c>states[<see cref="Lexer.RootStateId"/>]</c> holds <paramref name="rules"/>.</returns>
    internal static LexerRule[][] BuildSingleState(LexerRule[] rules) =>
        [rules];

    /// <summary>Counts the required and optional C-style rules.</summary>
    /// <param name="rules">Rules to count.</param>
    /// <returns>The number of rules present.</returns>
    private static int CountCStyleRules(in CStyleRuleSet rules)
    {
        const int RequiredRuleCount = 15;
        return RequiredRuleCount
            + (rules.DocComment is null ? 0 : 1)
            + (rules.Preprocessor is null ? 0 : 1)
            + (rules.SpecialString is null ? 0 : 1)
            + (rules.CharacterLiteral is null ? 0 : 1);
    }
}
