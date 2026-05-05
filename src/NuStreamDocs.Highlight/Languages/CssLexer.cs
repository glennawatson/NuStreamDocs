// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>CSS lexer.</summary>
/// <remarks>
/// Plain CSS — block comments only, no <c>//</c> line form, no variable sigil, no
/// parent-reference selector. SCSS-specific extensions live in <see cref="ScssLexer"/>.
/// </remarks>
public static class CssLexer
{
    /// <summary>Gets the singleton CSS lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the CSS lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        CssFamilyConfig config = new()
        {
            IncludeLineComment = false,
            VariableSigil = 0,
            IncludeParentSelector = false
        };

        return new(LanguageRuleBuilder.BuildSingleState(CssFamilyRules.Build(config)));
    }
}
