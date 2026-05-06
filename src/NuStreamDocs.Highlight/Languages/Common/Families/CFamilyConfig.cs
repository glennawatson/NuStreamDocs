// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Per-language configuration consumed by <see cref="CFamilyRules.Build"/>.</summary>
/// <remarks>
/// Built via object-initializer syntax so call sites read as
/// <c>new() { Tables = …, … }</c> — there is no multi-argument
/// constructor to drift out of order, and adding a new field never
/// breaks an existing call.
/// </remarks>
internal readonly record struct CFamilyConfig
{
    /// <summary>Gets the keyword + operator table bundle.</summary>
    public KeywordTablePack Tables { get; init; }

    /// <summary>Gets the structural punctuation byte set.</summary>
    public SearchValues<byte> Punctuation { get; init; }

    /// <summary>Gets the integer / hex literal suffix bytes (empty when the language has none).</summary>
    public SearchValues<byte> IntegerSuffix { get; init; }

    /// <summary>Gets the float literal suffix bytes (empty when the language has none).</summary>
    public SearchValues<byte> FloatSuffix { get; init; }

    /// <summary>Gets a value indicating whether <c>///</c> doc comments are recognized.</summary>
    public bool IncludeDocComment { get; init; }

    /// <summary>Gets a value indicating whether <c>#</c> preprocessor directives are recognized.</summary>
    public bool IncludePreprocessor { get; init; }

    /// <summary>Gets a value indicating whether <c>'x'</c> character literals are recognized.</summary>
    public bool IncludeCharacterLiteral { get; init; }

    /// <summary>Gets a value indicating whether the whitespace rule consumes line terminators.</summary>
    public bool WhitespaceIncludesNewlines { get; init; }

    /// <summary>Gets an optional rule emitted ahead of the regular double-string rule (e.g. raw / interpolated forms).</summary>
    public LexerRule? SpecialString { get; init; }

    /// <summary>Gets the optional identifier-start byte set; <see langword="null"/> falls back to the ASCII-letter / underscore default.</summary>
    public SearchValues<byte>? IdentifierFirst { get; init; }

    /// <summary>Gets the optional identifier-continuation byte set; <see langword="null"/> falls back to ASCII letters / digits / underscore.</summary>
    public SearchValues<byte>? IdentifierContinue { get; init; }
}
