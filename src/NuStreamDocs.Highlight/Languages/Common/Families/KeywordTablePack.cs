// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Shared keyword + operator table bundle used by family configs.</summary>
internal readonly record struct KeywordTablePack
{
    /// <summary>Gets the general-keyword set.</summary>
    public ByteKeywordSet Keywords { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for general keywords.</summary>
    public SearchValues<byte>? KeywordFirst { get; init; }

    /// <summary>Gets the type-keyword set.</summary>
    public ByteKeywordSet KeywordTypes { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for type keywords.</summary>
    public SearchValues<byte>? KeywordTypeFirst { get; init; }

    /// <summary>Gets the declaration-keyword set.</summary>
    public ByteKeywordSet KeywordDeclarations { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for declaration keywords.</summary>
    public SearchValues<byte>? KeywordDeclarationFirst { get; init; }

    /// <summary>Gets the constant-keyword set.</summary>
    public ByteKeywordSet KeywordConstants { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for constant keywords.</summary>
    public SearchValues<byte>? KeywordConstantFirst { get; init; }

    /// <summary>Gets the operator alternation, sorted longest-first.</summary>
    public byte[][] Operators { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for operators.</summary>
    public SearchValues<byte>? OperatorFirst { get; init; }
}
