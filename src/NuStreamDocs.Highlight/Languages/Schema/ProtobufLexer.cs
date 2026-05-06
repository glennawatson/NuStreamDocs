// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Schema;

/// <summary>Protocol Buffers (proto2 / proto3) schema lexer.</summary>
/// <remarks>
/// Schema-shape: <c>//</c> and <c>/* */</c> comments, <c>message</c> /
/// <c>enum</c> / <c>service</c> / <c>rpc</c> declarations, plus the standard
/// scalar-type keyword set.
/// </remarks>
public static class ProtobufLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "syntax package import option returns stream reserved extensions to"u8);

    /// <summary>Built-in scalar type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "double float int32 int64 uint32 uint64 sint32 sint64 fixed32 fixed64 sfixed32 sfixed64 bool string bytes"u8);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "message enum service rpc extend oneof map repeated optional required"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("true false"u8);

    /// <summary>Single-byte structural punctuation (includes <c>=</c> as the field-tag assignment).</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.<>="u8);

    /// <summary>Gets the singleton Protobuf lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Protobuf lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        SchemaFamilyConfig config = new()
        {
            IncludeHashComment = false,
            IncludeSlashComments = true,
            IncludeTripleQuotedString = false,
            IncludeSingleQuotedString = true,
            SigilFirst = null,
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
