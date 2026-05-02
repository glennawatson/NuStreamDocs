// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>F# lexer.</summary>
/// <remarks>
/// Pragmatic byte-shaped port of Pygments' <c>FSharpLexer</c>; rules
/// live in <see cref="FSharpRules"/> so embedded-language scenarios
/// (literate F# scripts, future Razor-with-F# components) can reuse
/// the same classification.
/// </remarks>
public static class FSharpLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(LanguageRuleBuilder.BuildSingleState(FSharpRules.Build()));
}
