// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Less lexer.</summary>
/// <remarks>
/// Adds <c>//</c> line comments, the <c>@variable</c> sigil (collides with CSS's at-rules — the
/// rule fires for both, classifying as <see cref="TokenClass.Keyword"/>), and the <c>&amp;</c>
/// parent-reference selector on top of the base CSS rule list.
/// </remarks>
public static class LessLexer
{
    /// <summary>Gets the singleton Less lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Less lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        CssFamilyConfig config = new()
        {
            IncludeLineComment = true,
            VariableSigil = (byte)'@',
            IncludeParentSelector = true
        };

        return new(LanguageRuleBuilder.BuildSingleState(CssFamilyRules.Build(config)));
    }
}
