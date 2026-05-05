// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Python lexer.</summary>
/// <remarks>
/// Pragmatic single-state subset of Pygments' <c>PythonLexer</c>; rules
/// live in <see cref="PythonRules"/> so future Python-embedding lexers
/// (Jupyter cells, doctest blocks) classify the same tokens with the
/// same Pygments-shape CSS classes.
/// </remarks>
public static class PythonLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(LanguageRuleBuilder.BuildSingleState(PythonRules.Build()));
}
