// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>C# lexer.</summary>
/// <remarks>
/// Modelled on Pygments' <c>CSharpLexer</c>; rules live in
/// <see cref="CSharpRules"/> so embedded-language scenarios
/// (Razor's <c>@code</c> blocks, future Blazor components) classify
/// the same tokens with the same Pygments-shape CSS classes.
/// </remarks>
public static class CSharpLexer
{
    /// <summary>Gets the singleton <see cref="Lexer"/> for C#.</summary>
    public static Lexer Instance { get; } = new(
        "csharp",
        LanguageRuleBuilder.BuildSingleState(CSharpRules.Build()));
}
