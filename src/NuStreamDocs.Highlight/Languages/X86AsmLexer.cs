// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>x86 / x86-64 assembly lexer (AT&amp;T and Intel syntaxes share enough that one lexer covers both).</summary>
/// <remarks>
/// Common opcode + register tables. Comments use either <c>;</c> (Intel-style) or
/// <c>#</c> (AT&amp;T after pre-processing); both are recognized.
/// </remarks>
public static class X86AsmLexer
{
    /// <summary>Common x86 / x86-64 mnemonic set (case-insensitive).</summary>
    private static readonly ByteKeywordSet Mnemonics = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "mov lea push pop add sub mul imul div idiv inc dec neg and or xor not shl shr sar rol ror"u8,
        "cmp test jmp je jne jz jnz jl jle jg jge jb jbe ja jae call ret int syscall sysret nop hlt"u8,
        "cld std cli sti lock rep repe repne movs stos lods scas cmps leave enter"u8);

    /// <summary>Common x86 / x86-64 register set (16/32/64-bit GPRs + RIP + segment + SSE/AVX hints).</summary>
    private static readonly ByteKeywordSet Registers = ByteKeywordSet.CreateFromSpaceSeparatedIgnoreCase(
        "rax rbx rcx rdx rsi rdi rbp rsp r8 r9 r10 r11 r12 r13 r14 r15 eax ebx ecx edx esi edi ebp esp ax bx cx dx si di bp sp al bl cl dl ah bh ch dh rip eip cs ds es fs gs ss"u8);

    /// <summary>First-byte set for line comments — both <c>;</c> (Intel) and <c>#</c> (AT&amp;T-after-cpp).</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create(";#"u8);

    /// <summary>Gets the singleton x86 / x86-64 assembly lexer.</summary>
    public static Lexer Instance { get; } = AsmFamilyRules.CreateLexer(new()
    {
        CommentFirst = CommentFirst,
        Mnemonics = Mnemonics,
        Registers = Registers,
        HexPrefix = true
    });
}
