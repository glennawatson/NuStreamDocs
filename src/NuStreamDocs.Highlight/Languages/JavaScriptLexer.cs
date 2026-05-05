// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>JavaScript lexer.</summary>
/// <remarks>
/// Reuses <see cref="TypeScriptLexer.BuildRules"/> — TypeScript is a
/// strict superset, and the JavaScript tokens we care about classify
/// into the same buckets. A future refinement could split out type-keyword
/// rules that JavaScript shouldn't recognize; the CSS classes don't
/// change either way.
/// </remarks>
public static class JavaScriptLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        LanguageRuleBuilder.BuildSingleState(TypeScriptLexer.BuildRules()));
}
