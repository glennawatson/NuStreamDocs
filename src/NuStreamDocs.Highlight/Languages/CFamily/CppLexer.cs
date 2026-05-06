// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>C++ lexer.</summary>
/// <remarks>
/// Extends C with template / class / namespace keywords and the
/// <c>R"delim(...)delim"</c> raw-string form (with optional
/// <c>L</c> / <c>u</c> / <c>u8</c> / <c>U</c> encoding prefix).
/// </remarks>
public static class CppLexer
{
    /// <summary>Length of the <c>u8</c> string-encoding prefix.</summary>
    private const int Utf8PrefixLength = 2;

    /// <summary>General-keyword set — shared C-family control-flow plus C++-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "goto"u8],
        [.. "sizeof"u8],
        [.. "typedef"u8],
        [.. "typeid"u8],
        [.. "typename"u8],
        [.. "decltype"u8],
        [.. "new"u8],
        [.. "delete"u8],
        [.. "this"u8],
        [.. "operator"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "xor"u8],
        [.. "alignof"u8],
        [.. "alignas"u8],
        [.. "co_await"u8],
        [.. "co_yield"u8],
        [.. "co_return"u8],
        [.. "concept"u8],
        [.. "requires"u8],
        [.. "static_cast"u8],
        [.. "dynamic_cast"u8],
        [.. "const_cast"u8],
        [.. "reinterpret_cast"u8]]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "char"u8],
        [.. "char8_t"u8],
        [.. "char16_t"u8],
        [.. "char32_t"u8],
        [.. "wchar_t"u8],
        [.. "short"u8],
        [.. "int"u8],
        [.. "long"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "void"u8],
        [.. "signed"u8],
        [.. "unsigned"u8],
        [.. "bool"u8],
        [.. "size_t"u8],
        [.. "ssize_t"u8],
        [.. "ptrdiff_t"u8],
        [.. "int8_t"u8],
        [.. "int16_t"u8],
        [.. "int32_t"u8],
        [.. "int64_t"u8],
        [.. "uint8_t"u8],
        [.. "uint16_t"u8],
        [.. "uint32_t"u8],
        [.. "uint64_t"u8],
        [.. "auto"u8]);

    /// <summary>Declaration / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "static"u8],
        [.. "extern"u8],
        [.. "const"u8],
        [.. "constexpr"u8],
        [.. "consteval"u8],
        [.. "constinit"u8],
        [.. "volatile"u8],
        [.. "inline"u8],
        [.. "mutable"u8],
        [.. "virtual"u8],
        [.. "explicit"u8],
        [.. "override"u8],
        [.. "final"u8],
        [.. "friend"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "class"u8],
        [.. "struct"u8],
        [.. "union"u8],
        [.. "enum"u8],
        [.. "namespace"u8],
        [.. "using"u8],
        [.. "template"u8],
        [.. "register"u8],
        [.. "thread_local"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nullptr"u8],
        [.. "NULL"u8]);

    /// <summary>Operator alternation — shared C-style core plus C++-specific multi-byte forms (<c>&lt;=&gt;</c>, <c>-&gt;*</c>, <c>.*</c>, <c>::</c>).</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<=>"u8],
        [.. "->*"u8],
        [.. ".*"u8],
        [.. "::"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>Integer-literal suffix bytes (extends the C set with <c>z</c> / <c>Z</c> for <c>size_t</c>).</summary>
    private static readonly SearchValues<byte> IntegerSuffixSet = SearchValues.Create("uUlLzZ"u8);

    /// <summary>First-byte set for the special-string rule (raw-string with optional encoding prefix).</summary>
    private static readonly SearchValues<byte> RawStringFirst = SearchValues.Create("RuLU"u8);

    /// <summary>Bytes that terminate the d-char delimiter run.</summary>
    private static readonly SearchValues<byte> RawStringDelimiterStop = SearchValues.Create("()\\\" \t\r\n"u8);

    /// <summary>Gets the singleton C++ lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the C++ lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        var rawString = new LexerRule(MatchRawOrPrefixedString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = RawStringFirst };

        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = CFamilyShared.StandardPunctuation,
            IntegerSuffix = IntegerSuffixSet,
            FloatSuffix = CFamilyShared.CFloatSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = true,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = rawString
        };

        return CFamilyRules.CreateLexer(config);
    }

    /// <summary>Matches a C++ raw-string literal — <c>R"delim(...)delim"</c> with an optional <c>L</c>/<c>u8</c>/<c>u</c>/<c>U</c> encoding prefix.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchRawOrPrefixedString(ReadOnlySpan<byte> slice)
    {
        var pos = ConsumeEncodingPrefix(slice);
        if (pos >= slice.Length || slice[pos] is not (byte)'R')
        {
            return 0;
        }

        pos++;
        if (pos >= slice.Length || slice[pos] is not (byte)'"')
        {
            return 0;
        }

        pos++;
        var delimStart = pos;
        var delimEnd = pos + slice[pos..].IndexOfAny(RawStringDelimiterStop);
        if (delimEnd < pos || slice[delimEnd] is not (byte)'(')
        {
            return 0;
        }

        return MatchRawStringBody(slice, delimEnd + 1, slice[delimStart..delimEnd]);
    }

    /// <summary>Consumes the optional encoding prefix (<c>L</c>, <c>u</c>, <c>u8</c>, or <c>U</c>) at the cursor.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Number of bytes consumed.</returns>
    private static int ConsumeEncodingPrefix(ReadOnlySpan<byte> slice)
    {
        if (slice is [(byte)'u', (byte)'8', ..])
        {
            return Utf8PrefixLength;
        }

        return slice is [(byte)'L' or (byte)'u' or (byte)'U', ..] ? 1 : 0;
    }

    /// <summary>Walks the raw-string body until <c>)delim"</c>.</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="bodyStart">Index of the first body byte (after the opening <c>(</c>).</param>
    /// <param name="delimiter">Delimiter bytes captured between <c>"</c> and <c>(</c>.</param>
    /// <returns>Total length matched on success, zero on unterminated input.</returns>
    private static int MatchRawStringBody(ReadOnlySpan<byte> slice, int bodyStart, ReadOnlySpan<byte> delimiter)
    {
        var pos = bodyStart;
        while (pos < slice.Length)
        {
            if (slice[pos] is not (byte)')')
            {
                pos++;
                continue;
            }

            var afterParen = pos + 1;
            if (afterParen + delimiter.Length < slice.Length
                && slice.Slice(afterParen, delimiter.Length).SequenceEqual(delimiter)
                && slice[afterParen + delimiter.Length] is (byte)'"')
            {
                return afterParen + delimiter.Length + 1;
            }

            pos++;
        }

        return 0;
    }
}
