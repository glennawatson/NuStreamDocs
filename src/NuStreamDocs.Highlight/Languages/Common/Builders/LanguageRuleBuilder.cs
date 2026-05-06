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
    public static LexerRule[] BuildCStyleRules(in CStyleRuleSet rules)
    {
        var count = 15;
        if (rules.DocComment is not null)
        {
            count++;
        }

        if (rules.Preprocessor is not null)
        {
            count++;
        }

        if (rules.SpecialString is not null)
        {
            count++;
        }

        if (rules.CharacterLiteral is not null)
        {
            count++;
        }

        var output = new LexerRule[count];
        var index = 0;
        output[index++] = rules.Whitespace;
        if (rules.DocComment is { } docComment)
        {
            output[index++] = docComment;
        }

        output[index++] = rules.LineComment;
        output[index++] = rules.BlockComment;
        if (rules.Preprocessor is { } preprocessor)
        {
            output[index++] = preprocessor;
        }

        if (rules.SpecialString is { } specialString)
        {
            output[index++] = specialString;
        }

        output[index++] = rules.DoubleString;
        output[index++] = rules.SingleString;
        if (rules.CharacterLiteral is { } characterLiteral)
        {
            output[index++] = characterLiteral;
        }

        output[index++] = rules.HexNumber;
        output[index++] = rules.FloatNumber;
        output[index++] = rules.IntegerNumber;
        output[index++] = rules.KeywordConstant;
        output[index++] = rules.KeywordType;
        output[index++] = rules.KeywordDeclaration;
        output[index++] = rules.Keyword;
        output[index++] = rules.Identifier;
        output[index++] = rules.Operator;
        output[index] = rules.Punctuation;
        return output;
    }

    /// <summary>Builds a state table for a single-state lexer.</summary>
    /// <param name="rules">Root-state rules.</param>
    /// <returns>State table indexed by state id; <c>states[<see cref="Lexer.RootStateId"/>]</c> holds <paramref name="rules"/>.</returns>
    public static LexerRule[][] BuildSingleState(LexerRule[] rules) =>
        [rules];
}
