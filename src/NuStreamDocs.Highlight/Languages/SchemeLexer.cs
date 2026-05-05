// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Scheme / Racket lexer.</summary>
/// <remarks>
/// Lisp-family lexer with <c>:keyword</c> literals (Racket convention) but no Clojure-style
/// data brackets — Scheme keeps the classic paren-only syntax.
/// </remarks>
public static class SchemeLexer
{
    /// <summary>Declaration forms.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "define"u8],
        [.. "define-syntax"u8],
        [.. "define-record-type"u8],
        [.. "define-struct"u8],
        [.. "lambda"u8],
        [.. "let"u8],
        [.. "let*"u8],
        [.. "letrec"u8],
        [.. "letrec*"u8],
        [.. "let-syntax"u8],
        [.. "letrec-syntax"u8],
        [.. "set!"u8]);

    /// <summary>Control-flow forms — shared Lisp-family core plus Scheme-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. LispFamilyShared.CommonKeywords,
        [.. "begin"u8],
        [.. "delay"u8],
        [.. "force"u8],
        [.. "quasiquote"u8],
        [.. "unquote"u8],
        [.. "syntax-rules"u8]]);

    /// <summary>Constants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "#t"u8],
        [.. "#f"u8],
        [.. "true"u8],
        [.. "false"u8]);

    /// <summary>First-byte set for declaration forms.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("dls"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdfinoquw"u8);

    /// <summary>First-byte set for the bare <c>true</c> / <c>false</c> constant rule (the <c>#t</c> / <c>#f</c> Scheme forms are handled by the <c>#</c>-dispatch character-literal path).</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tf"u8);

    /// <summary>Gets the singleton Scheme lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Scheme lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LispFamilyConfig config = new()
        {
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            Keywords = Keywords,
            KeywordFirst = KeywordFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            IncludeDataBrackets = false,
            IncludeColonKeyword = true
        };

        return LispFamilyRules.CreateLexer(config);
    }
}
