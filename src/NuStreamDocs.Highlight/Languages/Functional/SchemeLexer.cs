// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Functional;

/// <summary>Scheme / Racket lexer.</summary>
/// <remarks>
/// Lisp-family lexer with <c>:keyword</c> literals (Racket convention) but no Clojure-style
/// data brackets — Scheme keeps the classic paren-only syntax.
/// </remarks>
public static class SchemeLexer
{
    /// <summary>Declaration forms.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "define define-syntax define-record-type define-struct lambda let let* letrec letrec* let-syntax letrec-syntax set!"u8);

    /// <summary>Control-flow forms — shared Lisp-family core plus Scheme-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        LispFamilyShared.CommonKeywordsLiteral,
        "begin delay force quasiquote unquote syntax-rules"u8);

    /// <summary>Constants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(
        "#t #f true false"u8);

    /// <summary>Gets the singleton Scheme lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Scheme lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LispFamilyConfig config = new()
        {
            KeywordDeclarations = KeywordDeclarations,
            Keywords = Keywords,
            KeywordConstants = KeywordConstants,
            IncludeDataBrackets = false,
            IncludeColonKeyword = true
        };

        return LispFamilyRules.CreateLexer(config);
    }
}
