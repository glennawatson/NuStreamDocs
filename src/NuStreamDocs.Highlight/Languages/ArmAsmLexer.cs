// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>ARM / AArch64 assembly lexer.</summary>
/// <remarks>
/// ARM-toolchain comment style is <c>;</c> by tradition (some 32-bit GAS
/// dialects use <c>@</c>) — both first bytes are wired here.
/// </remarks>
public static class ArmAsmLexer
{
    /// <summary>ARM / AArch64 mnemonic set (case-insensitive).</summary>
    private static readonly ByteKeywordSet Mnemonics = ByteKeywordSet.CreateIgnoreCase(
        [.. "mov"u8],
        [.. "movz"u8],
        [.. "movk"u8],
        [.. "movn"u8],
        [.. "ldr"u8],
        [.. "str"u8],
        [.. "ldp"u8],
        [.. "stp"u8],
        [.. "ldur"u8],
        [.. "stur"u8],
        [.. "add"u8],
        [.. "sub"u8],
        [.. "mul"u8],
        [.. "udiv"u8],
        [.. "sdiv"u8],
        [.. "and"u8],
        [.. "orr"u8],
        [.. "eor"u8],
        [.. "lsl"u8],
        [.. "lsr"u8],
        [.. "asr"u8],
        [.. "cmp"u8],
        [.. "cmn"u8],
        [.. "tst"u8],
        [.. "b"u8],
        [.. "bl"u8],
        [.. "br"u8],
        [.. "blr"u8],
        [.. "ret"u8],
        [.. "beq"u8],
        [.. "bne"u8],
        [.. "blt"u8],
        [.. "ble"u8],
        [.. "bgt"u8],
        [.. "bge"u8],
        [.. "cbz"u8],
        [.. "cbnz"u8],
        [.. "tbz"u8],
        [.. "tbnz"u8],
        [.. "nop"u8],
        [.. "svc"u8],
        [.. "hvc"u8],
        [.. "smc"u8],
        [.. "dmb"u8],
        [.. "dsb"u8],
        [.. "isb"u8]);

    /// <summary>ARM / AArch64 register set (X0–X30, W0–W30, SP, LR, PC, FP).</summary>
    private static readonly ByteKeywordSet Registers = ByteKeywordSet.CreateIgnoreCase(
        [.. "x0"u8],
        [.. "x1"u8],
        [.. "x2"u8],
        [.. "x3"u8],
        [.. "x4"u8],
        [.. "x5"u8],
        [.. "x6"u8],
        [.. "x7"u8],
        [.. "x8"u8],
        [.. "x9"u8],
        [.. "x10"u8],
        [.. "x11"u8],
        [.. "x12"u8],
        [.. "x13"u8],
        [.. "x14"u8],
        [.. "x15"u8],
        [.. "x16"u8],
        [.. "x17"u8],
        [.. "x18"u8],
        [.. "x19"u8],
        [.. "x20"u8],
        [.. "x21"u8],
        [.. "x22"u8],
        [.. "x23"u8],
        [.. "x24"u8],
        [.. "x25"u8],
        [.. "x26"u8],
        [.. "x27"u8],
        [.. "x28"u8],
        [.. "x29"u8],
        [.. "x30"u8],
        [.. "w0"u8],
        [.. "w1"u8],
        [.. "w2"u8],
        [.. "w3"u8],
        [.. "w4"u8],
        [.. "w5"u8],
        [.. "w6"u8],
        [.. "w7"u8],
        [.. "w8"u8],
        [.. "sp"u8],
        [.. "lr"u8],
        [.. "pc"u8],
        [.. "fp"u8],
        [.. "xzr"u8],
        [.. "wzr"u8]);

    /// <summary>First-byte set for line comments — <c>;</c> for ARM tradition, <c>@</c> for some 32-bit GAS dialects.</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create(";@"u8);

    /// <summary>Gets the singleton ARM / AArch64 assembly lexer.</summary>
    public static Lexer Instance { get; } = AsmFamilyRules.CreateLexer(new()
    {
        CommentFirst = CommentFirst,
        Mnemonics = Mnemonics,
        Registers = Registers,
        HexPrefix = true
    });
}
