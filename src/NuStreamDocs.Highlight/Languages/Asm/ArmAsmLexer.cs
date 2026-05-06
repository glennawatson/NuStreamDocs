// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Asm;

/// <summary>ARM / AArch64 assembly lexer.</summary>
/// <remarks>
/// ARM-toolchain comment style is <c>;</c> by tradition (some 32-bit GAS
/// dialects use <c>@</c>) — both first bytes are wired here.
/// </remarks>
public static class ArmAsmLexer
{
    /// <summary>ARM / AArch64 mnemonic set (case-insensitive).</summary>
    private static readonly ByteKeywordSet Mnemonics = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "mov movz movk movn ldr str ldp stp ldur stur add sub mul udiv sdiv and orr eor lsl lsr asr"u8,
        "cmp cmn tst b bl br blr ret beq bne blt ble bgt bge cbz cbnz tbz tbnz nop svc hvc smc dmb dsb isb"u8);

    /// <summary>ARM / AArch64 register set (X0–X30, W0–W30, SP, LR, PC, FP).</summary>
    private static readonly ByteKeywordSet Registers = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "x0 x1 x2 x3 x4 x5 x6 x7 x8 x9 x10 x11 x12 x13 x14 x15 x16 x17 x18 x19 x20 x21 x22 x23 x24 x25 x26 x27 x28 x29 x30 w0 w1 w2 w3 w4 w5 w6 w7 w8 sp lr pc fp xzr wzr"u8);

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
