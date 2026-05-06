// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Data;

/// <summary>YAML lexer.</summary>
/// <remarks>
/// Pragmatic subset of the YAML grammar; rules live in
/// <see cref="YamlRules"/> so future YAML-embedding lexers
/// (Ansible playbooks, Helm chart templates, GitHub Actions workflows)
/// classify the same tokens with the same short-form CSS classes.
/// </remarks>
public static class YamlLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(LanguageRuleBuilder.BuildSingleState(YamlRules.Build()));
}
