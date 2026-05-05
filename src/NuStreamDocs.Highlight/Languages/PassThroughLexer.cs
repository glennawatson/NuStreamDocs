// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Builds a placeholder <see cref="Lexer"/> that classifies the whole
/// input as <see cref="TokenClass.Text"/>.
/// </summary>
/// <remarks>
/// Used for languages we register a name for but haven't fully ported
/// yet. Authors writing
/// <c>```rust</c> blocks get a registered lexer, escaped output, and
/// the same plumbing as a real lexer — only the per-language
/// classification is missing. Replacing the placeholder with a real
/// rule list lights up styling without any consumer-visible change.
/// </remarks>
public static class PassThroughLexer
{
    /// <summary>
    /// Gets the singleton lexer instance.
    /// </summary>
    public static Lexer Instance { get; } = new(LanguageRuleBuilder.BuildSingleState(
    [

        // Consume one byte per step — every byte is classified as plain text.
        new(static slice => slice is [_, ..] ? 1 : 0, TokenClass.Text, LexerRule.NoStateChange)
    ]));
}
