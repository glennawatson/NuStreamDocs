// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Scripting;

/// <summary>Bash / sh / zsh lexer.</summary>
/// <remarks>
/// Pragmatic subset of the Bash grammar; rules live in
/// <see cref="BashRules"/> so future shell-embedding lexers (Dockerfile,
/// GitHub Actions <c>run:</c> blocks) classify the same tokens with the
/// same short-form CSS classes.
/// </remarks>
public static class BashLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(LanguageRuleBuilder.BuildSingleState(BashRules.Build()));
}
