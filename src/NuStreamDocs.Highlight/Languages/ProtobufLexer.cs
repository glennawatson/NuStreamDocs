// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Protocol Buffers (proto2 / proto3) schema lexer.</summary>
/// <remarks>
/// Schema-shape: <c>//</c> and <c>/* */</c> comments, <c>message</c> /
/// <c>enum</c> / <c>service</c> / <c>rpc</c> declarations, plus the standard
/// scalar-type keyword set.
/// </remarks>
public static class ProtobufLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "syntax"u8],
        [.. "package"u8],
        [.. "import"u8],
        [.. "option"u8],
        [.. "returns"u8],
        [.. "stream"u8],
        [.. "reserved"u8],
        [.. "extensions"u8],
        [.. "to"u8]);

    /// <summary>Built-in scalar type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "double"u8],
        [.. "float"u8],
        [.. "int32"u8],
        [.. "int64"u8],
        [.. "uint32"u8],
        [.. "uint64"u8],
        [.. "sint32"u8],
        [.. "sint64"u8],
        [.. "fixed32"u8],
        [.. "fixed64"u8],
        [.. "sfixed32"u8],
        [.. "sfixed64"u8],
        [.. "bool"u8],
        [.. "string"u8],
        [.. "bytes"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "message"u8],
        [.. "enum"u8],
        [.. "service"u8],
        [.. "rpc"u8],
        [.. "extend"u8],
        [.. "oneof"u8],
        [.. "map"u8],
        [.. "repeated"u8],
        [.. "optional"u8],
        [.. "required"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8]);

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
