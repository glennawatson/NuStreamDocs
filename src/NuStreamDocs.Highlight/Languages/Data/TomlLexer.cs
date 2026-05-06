// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Data;

/// <summary>TOML lexer.</summary>
/// <remarks>
/// Section <c>[table]</c> and array-of-tables <c>[[table]]</c> headers, <c>#</c> line comments,
/// quoted strings, numeric literals, and the <c>true</c> / <c>false</c> boolean constants.
/// Date / datetime literals are not separately classified — they fall through as identifiers /
/// digits, which preserves the surface form without a parser-grade type system.
/// </remarks>
public static class TomlLexer
{
    /// <summary>First-byte set for TOML comments (<c>#</c> only).</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the key-value separator (<c>=</c>).</summary>
    private static readonly SearchValues<byte> SeparatorFirst = SearchValues.Create("="u8);

    /// <summary>TOML boolean constants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8]);

    /// <summary>First-byte set for the boolean-constant rule.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tf"u8);

    /// <summary>Gets the singleton TOML lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the TOML lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        IniFamilyConfig config = new()
        {
            CommentFirst = CommentFirst,
            SeparatorFirst = SeparatorFirst,
            RecognizeDoubleBracketHeader = true,
            RecognizeStringLiterals = true,
            RecognizeNumericLiterals = true,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst
        };

        return IniFamilyRules.CreateLexer(config);
    }
}
