// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Builders;

/// <summary>Per-language configuration consumed by <see cref="SingleStateLexerRules.Build"/>.</summary>
/// <remarks>
/// Generic single-state lexer shape: whitespace, optional doc / line / block / pre-string
/// rules, double / optional single string, integer / float, four keyword tables, identifier,
/// operator alternation, and structural punctuation. Bespoke lexers (Nim, Julia, MATLAB, R,
/// Lua, Erlang, Elixir, Ruby, …) all share this skeleton and only differ in the keyword
/// tables, the comment shape, and a few first-byte sets.
/// </remarks>
internal readonly record struct SingleStateLexerConfig
{
    /// <summary>Gets the whitespace-rule first-byte set (default <see cref="TokenMatchers.AsciiWhitespaceWithNewlines"/>).</summary>
    public SearchValues<byte>? WhitespaceFirst { get; init; }

    /// <summary>
    /// Gets the optional pre-string rule emitted ahead of the regular comment / string
    /// slots (used for special block-comment shapes that share a first byte with a
    /// line-comment marker).
    /// </summary>
    public LexerRule? PreCommentRule { get; init; }

    /// <summary>Gets the optional line-comment rule.</summary>
    public LexerRule? LineComment { get; init; }

    /// <summary>Gets the optional second line-comment rule (e.g. Bash <c>#!</c> shebang or VB.NET <c>'</c> quote).</summary>
    public LexerRule? AlternateLineComment { get; init; }

    /// <summary>Gets the optional block-comment rule.</summary>
    public LexerRule? BlockComment { get; init; }

    /// <summary>Gets the optional special-string rule emitted ahead of the regular string forms (raw / triple-quoted / sigil).</summary>
    public LexerRule? SpecialString { get; init; }

    /// <summary>Gets a value indicating whether the standard double-quoted backslash-escape string rule is included.</summary>
    public bool IncludeDoubleQuotedString { get; init; }

    /// <summary>Gets a value indicating whether the single-quoted backslash-escape string rule is included.</summary>
    public bool IncludeSingleQuotedString { get; init; }

    /// <summary>Gets the optional after-string rules emitted between strings and numbers (used for sigil-prefixed names like <c>$variable</c> or <c>:atom</c>); appended in order.</summary>
    public LexerRule[]? PostStringRules { get; init; }

    /// <summary>Gets the optional extra rules appended after operators and before punctuation (used for language-specific shapes the helper doesn't model).</summary>
    public LexerRule[]? ExtraRules { get; init; }

    /// <summary>Gets a value indicating whether the standard <c>1.0</c> float rule is included.</summary>
    public bool IncludeFloatLiteral { get; init; }

    /// <summary>Gets a value indicating whether the standard digit-run integer rule is included.</summary>
    public bool IncludeIntegerLiteral { get; init; }

    /// <summary>Gets a value indicating whether the signed <c>-1.0</c> float rule is emitted (used in place of the unsigned form when set).</summary>
    public bool IncludeSignedFloatLiteral { get; init; }

    /// <summary>Gets a value indicating whether the signed <c>-1</c> integer rule is emitted (used in place of the unsigned form when set).</summary>
    public bool IncludeSignedIntegerLiteral { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for the numeric literal rules; <see langword="null"/> falls back to <see cref="TokenMatchers.AsciiDigits"/>.</summary>
    public SearchValues<byte>? NumberFirst { get; init; }

    /// <summary>Gets the constant-keyword set; <see langword="null"/> disables the rule.</summary>
    public ByteKeywordSet? KeywordConstants { get; init; }

    /// <summary>Gets the first-byte dispatch set for <see cref="KeywordConstants"/>.</summary>
    public SearchValues<byte>? KeywordConstantFirst { get; init; }

    /// <summary>Gets the type-keyword set; <see langword="null"/> disables the rule.</summary>
    public ByteKeywordSet? KeywordTypes { get; init; }

    /// <summary>Gets the first-byte dispatch set for <see cref="KeywordTypes"/>.</summary>
    public SearchValues<byte>? KeywordTypeFirst { get; init; }

    /// <summary>Gets the declaration-keyword set; <see langword="null"/> disables the rule.</summary>
    public ByteKeywordSet? KeywordDeclarations { get; init; }

    /// <summary>Gets the first-byte dispatch set for <see cref="KeywordDeclarations"/>.</summary>
    public SearchValues<byte>? KeywordDeclarationFirst { get; init; }

    /// <summary>Gets the general-keyword set; <see langword="null"/> disables the rule.</summary>
    public ByteKeywordSet? Keywords { get; init; }

    /// <summary>Gets the first-byte dispatch set for <see cref="Keywords"/>.</summary>
    public SearchValues<byte>? KeywordFirst { get; init; }

    /// <summary>Gets the built-in / library-name keyword set classified as <see cref="TokenClass.NameBuiltin"/>; <see langword="null"/> disables the rule.</summary>
    public ByteKeywordSet? BuiltinKeywords { get; init; }

    /// <summary>Gets the first-byte dispatch set for <see cref="BuiltinKeywords"/>.</summary>
    public SearchValues<byte>? BuiltinKeywordFirst { get; init; }

    /// <summary>Gets a value indicating whether the four primary keyword rules require a line-start anchor (used by Makefile-shaped languages where directives must begin at column zero).</summary>
    public bool KeywordsRequireLineStart { get; init; }

    /// <summary>Gets the optional identifier-continuation set; <see langword="null"/> falls back to ASCII letters / digits / underscore.</summary>
    public SearchValues<byte>? IdentifierContinue { get; init; }

    /// <summary>Gets a value indicating whether the standard identifier rule is suppressed (for languages that emit their own identifier-shaped rules via <see cref="ExtraRules"/>).</summary>
    public bool SuppressIdentifierRule { get; init; }

    /// <summary>Gets the optional operator alternation, sorted longest-first; <see langword="null"/> disables the rule.</summary>
    public byte[][]? Operators { get; init; }

    /// <summary>
    /// Gets the optional first-byte dispatch set for <see cref="Operators"/>.
    /// </summary>
    public SearchValues<byte>? OperatorFirst { get; init; }

    /// <summary>Gets the structural punctuation byte set; <see langword="null"/> disables the rule.</summary>
    public SearchValues<byte>? Punctuation { get; init; }
}
