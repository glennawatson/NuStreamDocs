// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        CFamilyShared.CExtraKeywordsLiteral,
        "typeid typename decltype new delete this operator and or not xor alignof alignas co_await co_yield co_return concept requires static_cast dynamic_cast const_cast reinterpret_cast"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.CPrimitiveTypesLiteral,
        CFamilyShared.CSizedIntegerTypesLiteral,
        "char8_t char16_t char32_t wchar_t bool auto"u8);

    /// <summary>Declaration / qualifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "static extern const constexpr consteval constinit volatile inline mutable virtual explicit override final friend public private protected"u8,
        "class struct union enum namespace using template register thread_local"u8);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false nullptr NULL"u8);

    /// <summary>Operator alternation — shared C-style core plus C++-specific multi-byte forms (<c>&lt;=&gt;</c>, <c>-&gt;*</c>, <c>.*</c>, <c>::</c>).</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "<=> ->* .* ::"u8,
        CFamilyShared.StandardOperatorsLiteral);

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
