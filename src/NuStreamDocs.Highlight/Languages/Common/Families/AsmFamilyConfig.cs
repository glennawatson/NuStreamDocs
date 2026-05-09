// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Per-language configuration consumed by <see cref="AsmFamilyRules.Build"/>.</summary>
internal readonly record struct AsmFamilyConfig
{
    /// <summary>Gets the comment-introducer first-byte set (typically <c>;</c>, <c>#</c>, or <c>!</c>).</summary>
    public SearchValues<byte> CommentFirst { get; init; }

    /// <summary>Gets the mnemonic / opcode keyword set (case-insensitive lookup).</summary>
    public ByteKeywordSet Mnemonics { get; init; }

    /// <summary>Gets the register-name keyword set (case-insensitive lookup).</summary>
    public ByteKeywordSet Registers { get; init; }

    /// <summary>Gets a value indicating whether <c>0x...</c> hex literals are recognized.</summary>
    public bool HexPrefix { get; init; }
}
