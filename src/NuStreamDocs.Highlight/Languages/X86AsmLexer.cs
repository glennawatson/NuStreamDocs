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
    private static readonly ByteKeywordSet Mnemonics = ByteKeywordSet.CreateIgnoreCase(
        [.. "mov"u8],
        [.. "lea"u8],
        [.. "push"u8],
        [.. "pop"u8],
        [.. "add"u8],
        [.. "sub"u8],
        [.. "mul"u8],
        [.. "imul"u8],
        [.. "div"u8],
        [.. "idiv"u8],
        [.. "inc"u8],
        [.. "dec"u8],
        [.. "neg"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "xor"u8],
        [.. "not"u8],
        [.. "shl"u8],
        [.. "shr"u8],
        [.. "sar"u8],
        [.. "rol"u8],
        [.. "ror"u8],
        [.. "cmp"u8],
        [.. "test"u8],
        [.. "jmp"u8],
        [.. "je"u8],
        [.. "jne"u8],
        [.. "jz"u8],
        [.. "jnz"u8],
        [.. "jl"u8],
        [.. "jle"u8],
        [.. "jg"u8],
        [.. "jge"u8],
        [.. "jb"u8],
        [.. "jbe"u8],
        [.. "ja"u8],
        [.. "jae"u8],
        [.. "call"u8],
        [.. "ret"u8],
        [.. "int"u8],
        [.. "syscall"u8],
        [.. "sysret"u8],
        [.. "nop"u8],
        [.. "hlt"u8],
        [.. "cld"u8],
        [.. "std"u8],
        [.. "cli"u8],
        [.. "sti"u8],
        [.. "lock"u8],
        [.. "rep"u8],
        [.. "repe"u8],
        [.. "repne"u8],
        [.. "movs"u8],
        [.. "stos"u8],
        [.. "lods"u8],
        [.. "scas"u8],
        [.. "cmps"u8],
        [.. "leave"u8],
        [.. "enter"u8]);

    /// <summary>Common x86 / x86-64 register set (16/32/64-bit GPRs + RIP + segment + SSE/AVX hints).</summary>
    private static readonly ByteKeywordSet Registers = ByteKeywordSet.CreateIgnoreCase(
        [.. "rax"u8],
        [.. "rbx"u8],
        [.. "rcx"u8],
        [.. "rdx"u8],
        [.. "rsi"u8],
        [.. "rdi"u8],
        [.. "rbp"u8],
        [.. "rsp"u8],
        [.. "r8"u8],
        [.. "r9"u8],
        [.. "r10"u8],
        [.. "r11"u8],
        [.. "r12"u8],
        [.. "r13"u8],
        [.. "r14"u8],
        [.. "r15"u8],
        [.. "eax"u8],
        [.. "ebx"u8],
        [.. "ecx"u8],
        [.. "edx"u8],
        [.. "esi"u8],
        [.. "edi"u8],
        [.. "ebp"u8],
        [.. "esp"u8],
        [.. "ax"u8],
        [.. "bx"u8],
        [.. "cx"u8],
        [.. "dx"u8],
        [.. "si"u8],
        [.. "di"u8],
        [.. "bp"u8],
        [.. "sp"u8],
        [.. "al"u8],
        [.. "bl"u8],
        [.. "cl"u8],
        [.. "dl"u8],
        [.. "ah"u8],
        [.. "bh"u8],
        [.. "ch"u8],
        [.. "dh"u8],
        [.. "rip"u8],
        [.. "eip"u8],
        [.. "cs"u8],
        [.. "ds"u8],
        [.. "es"u8],
        [.. "fs"u8],
        [.. "gs"u8],
        [.. "ss"u8]);

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
