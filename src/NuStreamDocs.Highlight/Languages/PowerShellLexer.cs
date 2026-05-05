// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>PowerShell lexer.</summary>
/// <remarks>
/// Pragmatic single-state PowerShell lexer; rules live in
/// <see cref="PowerShellRules"/>. Case-insensitive across
/// keywords, dash-operators, verbs, and aliases — matching how
/// PowerShell itself tokenizes.
/// </remarks>
public static class PowerShellLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(LanguageRuleBuilder.BuildSingleState(PowerShellRules.Build()));
}
