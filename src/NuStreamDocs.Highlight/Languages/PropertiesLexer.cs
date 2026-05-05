// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Java <c>.properties</c> lexer.</summary>
/// <remarks>
/// Hash- or bang-prefixed line comments, <c>key = value</c> or <c>key : value</c> pairs.
/// No section headers (<c>.properties</c> is flat); no string-literal recognition (values
/// are free-form text).
/// </remarks>
public static class PropertiesLexer
{
    /// <summary>First-byte set for Properties comments (<c>#</c> and <c>!</c>).</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create("#!"u8);

    /// <summary>First-byte set for the key-value separator (<c>=</c> or <c>:</c>).</summary>
    private static readonly SearchValues<byte> SeparatorFirst = SearchValues.Create("=:"u8);

    /// <summary>Gets the singleton Properties lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Properties lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        IniFamilyConfig config = new()
        {
            CommentFirst = CommentFirst,
            SeparatorFirst = SeparatorFirst,
            RecognizeDoubleBracketHeader = false,
            RecognizeStringLiterals = false,
            RecognizeNumericLiterals = false,
            KeywordConstants = null,
            KeywordConstantFirst = null
        };

        return new(LanguageRuleBuilder.BuildSingleState(IniFamilyRules.Build(config)));
    }
}
