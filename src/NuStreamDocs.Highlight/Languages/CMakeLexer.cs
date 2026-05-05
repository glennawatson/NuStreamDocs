// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>CMake lexer.</summary>
/// <remarks>
/// <c>#</c> line comments, <c>#[[ ... ]]</c> block comments, the standard CMake
/// command set (<c>function</c> / <c>set</c> / <c>add_executable</c> / …),
/// <c>${VAR}</c> variable references, and <c>$&lt;…&gt;</c> generator expressions
/// folded into a single name token. Command names are case-insensitive in
/// CMake, so the keyword tables use the case-insensitive lookup variant.
/// </remarks>
public static class CMakeLexer
{
    /// <summary>Control-flow command set (case-insensitive).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateIgnoreCase(
        [.. "if"u8],
        [.. "elseif"u8],
        [.. "else"u8],
        [.. "endif"u8],
        [.. "while"u8],
        [.. "endwhile"u8],
        [.. "foreach"u8],
        [.. "endforeach"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "return"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "true"u8],
        [.. "false"u8],
        [.. "on"u8],
        [.. "off"u8],
        [.. "yes"u8],
        [.. "no"u8]);

    /// <summary>Declaration command set.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateIgnoreCase(
        [.. "function"u8],
        [.. "endfunction"u8],
        [.. "macro"u8],
        [.. "endmacro"u8],
        [.. "set"u8],
        [.. "unset"u8],
        [.. "option"u8],
        [.. "include"u8],
        [.. "include_directories"u8],
        [.. "find_package"u8],
        [.. "find_path"u8],
        [.. "find_program"u8],
        [.. "find_library"u8],
        [.. "find_file"u8],
        [.. "add_executable"u8],
        [.. "add_library"u8],
        [.. "add_custom_command"u8],
        [.. "add_custom_target"u8],
        [.. "add_dependencies"u8],
        [.. "add_subdirectory"u8],
        [.. "add_definitions"u8],
        [.. "add_compile_options"u8],
        [.. "add_compile_definitions"u8],
        [.. "target_link_libraries"u8],
        [.. "target_include_directories"u8],
        [.. "target_compile_options"u8],
        [.. "target_compile_definitions"u8],
        [.. "target_compile_features"u8],
        [.. "target_sources"u8],
        [.. "install"u8],
        [.. "configure_file"u8],
        [.. "project"u8],
        [.. "cmake_minimum_required"u8],
        [.. "cmake_policy"u8],
        [.. "message"u8]);

    /// <summary>First-byte set for whitespace.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the <c>#</c> comment dispatch.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the variable-expansion / generator-expression rule.</summary>
    private static readonly SearchValues<byte> DollarFirst = SearchValues.Create("$"u8);

    /// <summary>First-byte set for general keywords (lower + upper-case dispatch).</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcefinortwyABCEFINORTWY"u8);

    /// <summary>First-byte set for declaration keywords (lower + upper-case dispatch).</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acefimopstuACEFIMOPSTU"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("();,"u8);

    /// <summary>Identifier-continuation set — CMake identifiers may contain <c>-</c> and <c>.</c>.</summary>
    private static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-."u8);

    /// <summary>Gets the singleton CMake lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the CMake lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

            // #[[ ... ]] block comment must precede the # line-comment rule.
            new(MatchBlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = HashFirst },

            // # line comment to end-of-line.
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },

            // ${VAR} variable, $<...> generator-expression — emit as one name token.
            new(MatchDollarExpansion, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = DollarFirst },

            // "..." string with backslash escapes.
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

            // Numeric literal.
            new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            // Declaration commands (set, function, add_executable, ...).
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordDeclarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst },

            // Control-flow commands (if, while, foreach, ...).
            new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },

            new(
                static slice => TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, IdentifierContinue),
                TokenClass.Name,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches a CMake <c>#[[ ... ]]</c> block comment (with optional <c>=</c> level markers — same shape as Lua's long-bracket form).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice)
    {
        const int HashOpenLength = 1;
        if (slice.Length < HashOpenLength + 1 || slice[0] is not (byte)'#' || slice[1] is not (byte)'[')
        {
            return 0;
        }

        var rest = slice[HashOpenLength..];
        var pos = 1;
        while (pos < rest.Length && rest[pos] is (byte)'=')
        {
            pos++;
        }

        if (pos >= rest.Length || rest[pos] is not (byte)'[')
        {
            return 0;
        }

        var levelCount = pos - 1;
        var bodyEnd = ScanLongBracketClose(rest, pos + 1, levelCount);
        return bodyEnd is 0 ? 0 : HashOpenLength + bodyEnd;
    }

    /// <summary>Walks the bracketed body until <c>]</c> + <paramref name="levelCount"/> <c>=</c>s + <c>]</c>.</summary>
    /// <param name="slice">Body slice (after the opening <c>[</c>).</param>
    /// <param name="bodyStart">Index of the first body byte.</param>
    /// <param name="levelCount">Number of <c>=</c>s required between the closing brackets.</param>
    /// <returns>Index past the closer, or zero on unterminated input.</returns>
    private static int ScanLongBracketClose(ReadOnlySpan<byte> slice, int bodyStart, int levelCount)
    {
        var pos = bodyStart;
        while (pos < slice.Length)
        {
            if (slice[pos] is not (byte)']')
            {
                pos++;
                continue;
            }

            var probe = pos + 1;
            var matched = 0;
            while (matched < levelCount && probe < slice.Length && slice[probe] is (byte)'=')
            {
                matched++;
                probe++;
            }

            if (matched == levelCount && probe < slice.Length && slice[probe] is (byte)']')
            {
                return probe + 1;
            }

            pos++;
        }

        return 0;
    }

    /// <summary>Matches CMake variable expansions (<c>${VAR}</c>) and generator expressions (<c>$&lt;…&gt;</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchDollarExpansion(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 2 || slice[0] is not (byte)'$')
        {
            return 0;
        }

        // ${VAR} environment-or-cache variable.
        var brace = TokenMatchers.MatchPrefixedBracketedBlock(slice, (byte)'$', (byte)'{', (byte)'}');
        if (brace > 0)
        {
            return brace;
        }

        // $<...> generator expression.
        return TokenMatchers.MatchPrefixedBracketedBlock(slice, (byte)'$', (byte)'<', (byte)'>');
    }
}
