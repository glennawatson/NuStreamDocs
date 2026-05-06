// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Per-language configuration consumed by <see cref="SchemaFamilyRules.Build"/>.</summary>
/// <remarks>
/// Schema-shape languages (GraphQL, Protobuf, HCL, Cue, Thrift, JSON-Schema)
/// share a flat shape of declarations / identifiers / strings / numbers /
/// braces. Comment style, sigil bytes, operator/punctuation tables, and
/// triple-quote handling are the only knobs that vary.
/// </remarks>
internal readonly record struct SchemaFamilyConfig
{
    /// <summary>Gets a value indicating whether <c>#</c> line comments are recognized.</summary>
    public bool IncludeHashComment { get; init; }

    /// <summary>Gets a value indicating whether <c>//</c> line comments and <c>/* */</c> block comments are recognized.</summary>
    public bool IncludeSlashComments { get; init; }

    /// <summary>Gets a value indicating whether the GraphQL <c>"""description"""</c> triple-quoted block-string form is recognized.</summary>
    public bool IncludeTripleQuotedString { get; init; }

    /// <summary>Gets a value indicating whether single-quoted strings are recognized in addition to double-quoted.</summary>
    public bool IncludeSingleQuotedString { get; init; }

    /// <summary>Gets the optional sigil-prefixed name byte set (<c>$@</c> for GraphQL <c>$variable</c> / <c>@directive</c>); empty disables the rule.</summary>
    public SearchValues<byte>? SigilFirst { get; init; }

    /// <summary>Gets the general-keyword set.</summary>
    public ByteKeywordSet Keywords { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for general keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordFirst { get; init; }

    /// <summary>Gets the type-keyword set.</summary>
    public ByteKeywordSet KeywordTypes { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for type keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordTypeFirst { get; init; }

    /// <summary>Gets the declaration-keyword set.</summary>
    public ByteKeywordSet KeywordDeclarations { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for declaration keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordDeclarationFirst { get; init; }

    /// <summary>Gets the constant-keyword set.</summary>
    public ByteKeywordSet KeywordConstants { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for constant keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordConstantFirst { get; init; }

    /// <summary>Gets the optional operator alternation, sorted longest-first; <see langword="null"/> disables the rule.</summary>
    public byte[][]? Operators { get; init; }

    /// <summary>Gets the first-byte dispatch set for operators; only meaningful when <see cref="Operators"/> is non-null.</summary>
    public SearchValues<byte>? OperatorFirst { get; init; }

    /// <summary>Gets the structural punctuation byte set.</summary>
    public SearchValues<byte> Punctuation { get; init; }
}
