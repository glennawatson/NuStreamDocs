// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Rust lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the
/// <c>r"..."</c> / <c>r#"..."#</c> raw-string and <c>b"..."</c> byte-string
/// forms folded into the special-string rule.
/// </remarks>
public static class RustLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus Rust-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "as"u8],
        [.. "in"u8],
        [.. "loop"u8],
        [.. "match"u8],
        [.. "yield"u8],
        [.. "where"u8],
        [.. "async"u8],
        [.. "await"u8],
        [.. "move"u8],
        [.. "dyn"u8],
        [.. "unsafe"u8],
        [.. "extern"u8],
        [.. "ref"u8],
        [.. "self"u8],
        [.. "Self"u8],
        [.. "super"u8],
        [.. "crate"u8]]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "bool"u8],
        [.. "char"u8],
        [.. "str"u8],
        [.. "i8"u8],
        [.. "i16"u8],
        [.. "i32"u8],
        [.. "i64"u8],
        [.. "i128"u8],
        [.. "isize"u8],
        [.. "u8"u8],
        [.. "u16"u8],
        [.. "u32"u8],
        [.. "u64"u8],
        [.. "u128"u8],
        [.. "usize"u8],
        [.. "f32"u8],
        [.. "f64"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "let"u8],
        [.. "mut"u8],
        [.. "const"u8],
        [.. "static"u8],
        [.. "fn"u8],
        [.. "struct"u8],
        [.. "enum"u8],
        [.. "trait"u8],
        [.. "impl"u8],
        [.. "type"u8],
        [.. "mod"u8],
        [.. "use"u8],
        [.. "pub"u8],
        [.. "union"u8]);

    /// <summary>Constant keywords — boolean / option / result variants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "None"u8],
        [.. "Some"u8],
        [.. "Ok"u8],
        [.. "Err"u8]);

    /// <summary>Operator alternation — shared C-style core plus Rust's range / arrow / path forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "..="u8],
        [.. ".."u8],
        [.. "=>"u8],
        [.. "::"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefilmrswuy"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("biuf"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("lmcsftueipu"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfNSOE"u8);

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
            Keywords = Keywords,
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = IntegerSuffixSet,
            FloatSuffix = FloatSuffixSet,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = specialString
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
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
