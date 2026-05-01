// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Builds a placeholder <see cref="Lexer"/> that classifies the whole
/// input as <see cref="TokenClass.Text"/>.
/// </summary>
/// <remarks>
/// Used for languages we register against the Pygments-equivalent name
/// set but haven't fully ported yet. Authors writing
/// <c>```rust</c> blocks get a registered lexer, escaped output, and
/// the same plumbing as a real lexer — only the per-language
/// classification is missing. Replacing the placeholder with a real
/// rule list lights up styling without any consumer-visible change.
/// </remarks>
public static partial class PassThroughLexer
{
    /// <summary>Builds a pass-through lexer for <paramref name="languageName"/>.</summary>
    /// <param name="languageName">Language identifier the registry will key on.</param>
    /// <returns>A configured lexer whose only rule emits a single character per step.</returns>
    public static Lexer Create(string languageName)
    {
        ArgumentException.ThrowIfNullOrEmpty(languageName);
        var states = new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = [new(AnyCharRegex(), TokenClass.Text, NextState: null)],
        }.ToFrozenDictionary(StringComparer.Ordinal);
        return new(languageName, states);
    }

    /// <summary>Cursor-anchored single-character matcher shared across every pass-through lexer.</summary>
    /// <returns>The compiled regex.</returns>
    [GeneratedRegex(@"\G[\s\S]", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex AnyCharRegex();
}
