// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Shared keyword / operator / constant byte tables for the C-family lexers.</summary>
/// <remarks>
/// Pass the <c>*Literal</c> spans to
/// <see cref="ByteKeywordSet.CreateFromSpaceSeparated(System.ReadOnlySpan{byte}, System.ReadOnlySpan{byte})"/>
/// (or <see cref="OperatorAlternationFactory.SplitLongestFirst(System.ReadOnlySpan{byte},System.ReadOnlySpan{byte})"/>)
/// when assembling per-language tables so the duplicated control-flow / operator entries only appear once across the project.
/// </remarks>
internal static class CFamilyShared
{
    /// <summary>Control-flow keywords every brace-style C-family language ships with.</summary>
    public static readonly byte[][] ControlFlow =
    [
        [.. "if"u8],
        [.. "else"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "switch"u8],
        [.. "case"u8],
        [.. "default"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "return"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8]
    ];

    /// <summary>
    /// First-byte dispatch set covering every byte that any <see cref="ControlFlow"/> entry
    /// starts with (<c>b</c>, <c>c</c>, <c>d</c>, <c>e</c>, <c>f</c>, <c>i</c>, <c>r</c>,
    /// <c>s</c>, <c>t</c>, <c>w</c>).
    /// </summary>
    public static readonly SearchValues<byte> ControlFlowFirst = SearchValues.Create("bcdefirstw"u8);

    /// <summary>The canonical <c>true</c> / <c>false</c> / <c>null</c> constant triple.</summary>
    public static readonly byte[][] TrueFalseNull =
    [
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]
    ];

    /// <summary>First-byte dispatch set for <see cref="TrueFalseNull"/> (<c>t</c>, <c>f</c>, <c>n</c>).</summary>
    public static readonly SearchValues<byte> TrueFalseNullFirst = SearchValues.Create("tfn"u8);

    /// <summary>Standard C-style operator alternation, sorted longest-first. Covers every operator C/C++/Java/Kotlin/Scala/Groovy/Dart/Swift/Zig/Rust/Go/etc. share.</summary>
    public static readonly byte[][] StandardOperators = OperatorAlternationFactory.SplitLongestFirst(
        "<<= >>= -> ++ -- == != <= >= && || << >> += -= *= /= %= &= |= ^= + - * / % & | ^ ! ~ = < > ?"u8);

    /// <summary>
    /// First-byte dispatch set for <see cref="StandardOperators"/>. Includes <c>:</c>
    /// and <c>.</c> so the operator rule fires for <c>::</c>, <c>?:</c>, <c>?.</c>,
    /// <c>..</c>, <c>..=</c>, <c>..&lt;</c>, and similar across the C-family
    /// dialects; on a miss the cursor falls through to the punctuation rule, so
    /// the broader dispatch set is correctness-preserving.
    /// </summary>
    public static readonly SearchValues<byte> StandardOperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?:."u8);

    /// <summary>C-style structural punctuation (<c>(</c>, <c>)</c>, <c>{</c>, <c>}</c>, <c>[</c>, <c>]</c>, <c>;</c>, <c>,</c>, <c>.</c>, <c>:</c>).</summary>
    public static readonly SearchValues<byte> StandardPunctuation = LanguageCommon.CCurlyPunctuationFirst;

    /// <summary>Structural punctuation plus the annotation marker <c>@</c> (Java / Kotlin / Scala / Groovy / Dart shape).</summary>
    public static readonly SearchValues<byte> AnnotationPunctuation = SearchValues.Create("(){}[];,.@"u8);

    /// <summary>Structural punctuation plus <c>:</c> and the annotation marker <c>@</c> (Swift / V / Zig / Crystal / Julia / Nim shape).</summary>
    public static readonly SearchValues<byte> AnnotationColonPunctuation = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Common JVM-style integer / hex literal suffix bytes (<c>l</c>, <c>L</c>).</summary>
    public static readonly SearchValues<byte> JvmIntegerSuffix = SearchValues.Create("lL"u8);

    /// <summary>Common JVM-style float-literal suffix bytes (<c>f</c>, <c>F</c>, <c>d</c>, <c>D</c>).</summary>
    public static readonly SearchValues<byte> JvmFloatSuffix = SearchValues.Create("fFdD"u8);

    /// <summary>Common C-style integer / hex literal suffix bytes (<c>u</c>, <c>U</c>, <c>l</c>, <c>L</c>).</summary>
    public static readonly SearchValues<byte> CIntegerSuffix = SearchValues.Create("uUlL"u8);

    /// <summary>Common C-style float-literal suffix bytes (<c>f</c>, <c>F</c>, <c>l</c>, <c>L</c>).</summary>
    public static readonly SearchValues<byte> CFloatSuffix = SearchValues.Create("fFlL"u8);

    /// <summary>Gets the control-flow keywords every brace-style C-family language ships with, as a space-separated literal.</summary>
    public static ReadOnlySpan<byte> ControlFlowLiteral =>
        "if else for while do switch case default break continue return throw try catch finally"u8;

    /// <summary>Gets the canonical <c>true</c> / <c>false</c> / <c>null</c> constant triple as a space-separated literal.</summary>
    public static ReadOnlySpan<byte> TrueFalseNullLiteral => "true false null"u8;

    /// <summary>Gets the <c>true</c> / <c>false</c> / <c>nil</c> constant triple (Swift / Crystal / Nim / Lua) as a space-separated literal.</summary>
    public static ReadOnlySpan<byte> TrueFalseNilLiteral => "true false nil"u8;

    /// <summary>Gets the canonical C primitive-type set (<c>char short int long float double void signed unsigned</c>) as a space-separated literal.</summary>
    public static ReadOnlySpan<byte> CPrimitiveTypesLiteral => "char short int long float double void signed unsigned"u8;

    /// <summary>
    /// Gets the standard C99/C11 sized-integer typedef set (<c>size_t</c>, <c>ssize_t</c>, <c>ptrdiff_t</c>,
    /// <c>int8_t</c>..<c>int64_t</c>, <c>uint8_t</c>..<c>uint64_t</c>) as a space-separated literal.
    /// </summary>
    public static ReadOnlySpan<byte> CSizedIntegerTypesLiteral =>
        "size_t ssize_t ptrdiff_t int8_t int16_t int32_t int64_t uint8_t uint16_t uint32_t uint64_t"u8;

    /// <summary>Gets the C / ObjC / C++ extra <c>goto sizeof typedef</c> keyword fragment as a space-separated literal.</summary>
    public static ReadOnlySpan<byte> CExtraKeywordsLiteral => "goto sizeof typedef"u8;

    /// <summary>Gets the standard C-style operator alternation as a space-separated literal (lengths arbitrary; the alternation factory orders longest-first).</summary>
    public static ReadOnlySpan<byte> StandardOperatorsLiteral =>
        "<<= >>= -> ++ -- == != <= >= && || << >> += -= *= /= %= &= |= ^= + - * / % & | ^ ! ~ = < > ?"u8;
}
