// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>WebAssembly text format (WAT) lexer.</summary>
/// <remarks>
/// Line comments use <c>;;</c>; classified through the <see cref="AsmFamilyRules"/>
/// shape with a <c>;</c> first-byte. Standard <c>i32</c>/<c>i64</c>/<c>f32</c>/<c>f64</c>
/// instruction families are recognized as mnemonics; <c>$variable</c> identifiers
/// classify as plain names.
/// </remarks>
public static class WatLexer
{
    /// <summary>Common WAT instruction set.</summary>
    private static readonly ByteKeywordSet Mnemonics = ByteKeywordSet.Create(
        [.. "module"u8],
        [.. "func"u8],
        [.. "param"u8],
        [.. "result"u8],
        [.. "local"u8],
        [.. "global"u8],
        [.. "memory"u8],
        [.. "table"u8],
        [.. "data"u8],
        [.. "elem"u8],
        [.. "import"u8],
        [.. "export"u8],
        [.. "type"u8],
        [.. "start"u8],
        [.. "block"u8],
        [.. "loop"u8],
        [.. "if"u8],
        [.. "else"u8],
        [.. "end"u8],
        [.. "br"u8],
        [.. "br_if"u8],
        [.. "br_table"u8],
        [.. "return"u8],
        [.. "call"u8],
        [.. "call_indirect"u8],
        [.. "drop"u8],
        [.. "select"u8],
        [.. "nop"u8],
        [.. "unreachable"u8],
        [.. "i32.const"u8],
        [.. "i64.const"u8],
        [.. "f32.const"u8],
        [.. "f64.const"u8],
        [.. "i32.add"u8],
        [.. "i32.sub"u8],
        [.. "i32.mul"u8],
        [.. "i32.eq"u8],
        [.. "i32.lt_s"u8],
        [.. "i32.lt_u"u8],
        [.. "i64.add"u8],
        [.. "i64.sub"u8],
        [.. "i64.mul"u8],
        [.. "f32.add"u8],
        [.. "f64.add"u8],
        [.. "local.get"u8],
        [.. "local.set"u8],
        [.. "local.tee"u8],
        [.. "global.get"u8],
        [.. "global.set"u8],
        [.. "i32.load"u8],
        [.. "i32.store"u8],
        [.. "i64.load"u8],
        [.. "i64.store"u8]);

    /// <summary>WAT type-keyword set (numeric types double as register-style annotations).</summary>
    private static readonly ByteKeywordSet Types = ByteKeywordSet.Create(
        [.. "i32"u8],
        [.. "i64"u8],
        [.. "f32"u8],
        [.. "f64"u8],
        [.. "v128"u8],
        [.. "funcref"u8],
        [.. "externref"u8]);

    /// <summary>First-byte set for the <c>;;</c> line-comment form (the second <c>;</c> falls inside the line-length scan).</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create(";"u8);

    /// <summary>Gets the singleton WAT lexer.</summary>
    public static Lexer Instance { get; } = AsmFamilyRules.CreateLexer(new()
    {
        CommentFirst = CommentFirst,
        Mnemonics = Mnemonics,
        Registers = Types,
        HexPrefix = true
    });
}
