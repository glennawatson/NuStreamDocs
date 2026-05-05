// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>INI / .editorconfig / .gitconfig / systemd-unit lexer.</summary>
/// <remarks>
/// Section headers, <c>;</c>/<c>#</c> line comments, <c>key = value</c> pairs.
/// Quoted string literals are recognized; numeric literals are not — INI values
/// are usually free-form so a bare <c>3.14</c> stays plain text and themes don't
/// over-color non-numeric values.
/// </remarks>
public static class IniLexer
{
    /// <summary>First-byte set for INI comment introducers (<c>;</c> and <c>#</c>).</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create(";#"u8);

    /// <summary>First-byte set for the key-value separator (<c>=</c>).</summary>
    private static readonly SearchValues<byte> SeparatorFirst = SearchValues.Create("="u8);

    /// <summary>Gets the singleton INI lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the INI lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        IniFamilyConfig config = new()
        {
            CommentFirst = CommentFirst,
            SeparatorFirst = SeparatorFirst,
            RecognizeDoubleBracketHeader = false,
            RecognizeStringLiterals = true,
            RecognizeNumericLiterals = false,
            KeywordConstants = null,
            KeywordConstantFirst = null
        };

        return IniFamilyRules.CreateLexer(config);
    }
}
