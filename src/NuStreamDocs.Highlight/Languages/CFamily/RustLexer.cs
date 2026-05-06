// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Rust lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the
/// <c>r"..."</c> / <c>r#"..."#</c> raw-string and <c>b"..."</c> byte-string
/// forms folded into the special-string rule.
/// </remarks>
public static class RustLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus Rust-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "as in loop match yield where async await move dyn unsafe extern ref self Self super crate"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "bool char str i8 i16 i32 i64 i128 isize u8 u16 u32 u64 u128 usize f32 f64"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "let mut const static fn struct enum trait impl type mod use pub union"u8);

    /// <summary>Constant keywords — boolean / option / result variants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "true false None Some Ok Err"u8);

    /// <summary>Operator alternation — shared C-style core plus Rust's range / arrow / path forms.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "..= .. => ::"u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Single-byte structural punctuation — shared C-curly set plus the Rust <c>#</c> attribute marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:#"u8);

    /// <summary>Integer-literal suffix bytes.</summary>
    private static readonly SearchValues<byte> IntegerSuffixSet = SearchValues.Create("iuf_0123456789"u8);

    /// <summary>Float-literal suffix bytes.</summary>
    private static readonly SearchValues<byte> FloatSuffixSet = SearchValues.Create("f"u8);

    /// <summary>First-byte set for the special-string rule (<c>r</c> for raw, <c>b</c> for byte-string).</summary>
    private static readonly SearchValues<byte> SpecialStringFirst = SearchValues.Create("rb"u8);

    /// <summary>Gets the singleton Rust lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Rust lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        var specialString = new LexerRule(MatchRustSpecialString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = SpecialStringFirst };

        CFamilyConfig config = new()
        {
            Tables = new()
            {
                Keywords = Keywords,
                KeywordTypes = KeywordTypes,
                KeywordDeclarations = KeywordDeclarations,
                KeywordConstants = KeywordConstants,
                Operators = OperatorTable,
                OperatorFirst = CFamilyShared.StandardOperatorFirst
            },
            Punctuation = PunctuationSet,
            IntegerSuffix = IntegerSuffixSet,
            FloatSuffix = FloatSuffixSet,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = specialString
        };

        return CFamilyRules.CreateLexer(config);
    }

    /// <summary>Matches Rust's raw-string and byte-string forms — <c>r"..."</c>, <c>r#"..."#</c>, <c>b"..."</c>, <c>br"..."</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchRustSpecialString(ReadOnlySpan<byte> slice)
    {
        var pos = 0;
        var hasBytePrefix = pos < slice.Length && slice[pos] is (byte)'b';
        if (hasBytePrefix)
        {
            pos++;
        }

        var hasRawPrefix = pos < slice.Length && slice[pos] is (byte)'r';
        if (hasRawPrefix)
        {
            pos++;
        }

        if (!hasRawPrefix)
        {
            // Plain b"..." with backslash escapes.
            if (!hasBytePrefix)
            {
                return 0;
            }

            var quoted = TokenMatchers.MatchDoubleQuotedWithBackslashEscape(slice[pos..]);
            return quoted is 0 ? 0 : pos + quoted;
        }

        return MatchRustRawStringTail(slice, pos);
    }

    /// <summary>Matches the raw-string tail starting after the optional <c>b</c>/<c>r</c> prefixes.</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="pos">Cursor position after the prefix run.</param>
    /// <returns>Total length matched (including prefix), or zero.</returns>
    private static int MatchRustRawStringTail(ReadOnlySpan<byte> slice, int pos)
    {
        var hashCount = 0;
        while (pos < slice.Length && slice[pos] is (byte)'#')
        {
            pos++;
            hashCount++;
        }

        if (pos >= slice.Length || slice[pos] is not (byte)'"')
        {
            return 0;
        }

        return MatchRustRawStringBody(slice, pos + 1, hashCount);
    }

    /// <summary>Walks the body of a raw string until a closing <c>"</c> followed by exactly <paramref name="hashCount"/> <c>#</c>s.</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="bodyStart">Index of the first body byte (after the opening quote).</param>
    /// <param name="hashCount">Number of trailing <c>#</c>s the closer must match.</param>
    /// <returns>Total length matched on success, zero on unterminated input.</returns>
    private static int MatchRustRawStringBody(ReadOnlySpan<byte> slice, int bodyStart, int hashCount)
    {
        var i = bodyStart;
        while (i < slice.Length)
        {
            if (slice[i] is not (byte)'"')
            {
                i++;
                continue;
            }

            if (TrailingHashesMatch(slice, i + 1, hashCount))
            {
                return i + 1 + hashCount;
            }

            i++;
        }

        return 0;
    }

    /// <summary>True when <paramref name="slice"/> has at least <paramref name="hashCount"/> <c>#</c> bytes starting at <paramref name="start"/>.</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="start">Index to check.</param>
    /// <param name="hashCount">Required hash count.</param>
    /// <returns>True on match.</returns>
    private static bool TrailingHashesMatch(ReadOnlySpan<byte> slice, int start, int hashCount)
    {
        if (start + hashCount > slice.Length)
        {
            return false;
        }

        for (var i = 0; i < hashCount; i++)
        {
            if (slice[start + i] is not (byte)'#')
            {
                return false;
            }
        }

        return true;
    }
}
