// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Schema;

/// <summary>GraphQL schema and query lexer.</summary>
/// <remarks>
/// Schema-shape language with <c>#</c> line comments, <c>type</c> / <c>scalar</c>
/// / <c>enum</c> / <c>interface</c> / <c>union</c> declarations, <c>$variable</c>
/// references, and the <c>!</c> non-null marker.
/// </remarks>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "GraphQL is a registered trademark.")]
public static class GraphQLLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "query mutation subscription fragment on schema directive extend implements repeatable"u8);

    /// <summary>Built-in scalar type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Int Float String Boolean ID"u8);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "type scalar enum interface union input"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("true false null"u8);

    /// <summary>First-byte set for the <c>$variable</c> / <c>@directive</c> sigils.</summary>
    private static readonly SearchValues<byte> SigilFirst = SearchValues.Create("$@"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[],:!|&="u8);

    /// <summary>Gets the singleton GraphQL lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the GraphQL lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        SchemaFamilyConfig config = new()
        {
            IncludeHashComment = true,
            IncludeSlashComments = false,
            IncludeTripleQuotedString = true,
            IncludeSingleQuotedString = false,
            SigilFirst = SigilFirst,
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = null,
            Punctuation = PunctuationSet
        };

        return SchemaFamilyRules.CreateLexer(config);
    }
}
