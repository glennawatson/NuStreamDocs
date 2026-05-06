// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Clojure lexer.</summary>
/// <remarks>
/// Lisp-family lexer with the Clojure-specific data brackets <c>[]</c> / <c>{}</c>,
/// <c>:keyword</c> literals, and the <c>defn</c> / <c>def</c> / <c>defmacro</c>
/// declaration set.
/// </remarks>
public static class ClojureLexer
{
    /// <summary>Declaration / structure forms.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "def"u8],
        [.. "defn"u8],
        [.. "defn-"u8],
        [.. "defmacro"u8],
        [.. "defmulti"u8],
        [.. "defmethod"u8],
        [.. "defprotocol"u8],
        [.. "defrecord"u8],
        [.. "deftype"u8],
        [.. "defstruct"u8],
        [.. "ns"u8]);

    /// <summary>Control-flow / binding forms.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "if-not"u8],
        [.. "when"u8],
        [.. "when-not"u8],
        [.. "cond"u8],
        [.. "case"u8],
        [.. "do"u8],
        [.. "let"u8],
        [.. "letfn"u8],
        [.. "loop"u8],
        [.. "recur"u8],
        [.. "fn"u8],
        [.. "lambda"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "binding"u8],
        [.. "doseq"u8],
        [.. "dotimes"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "quote"u8],
        [.. "var"u8]);

    /// <summary>Constants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8]);

    /// <summary>Gets the singleton Clojure lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Clojure lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LispFamilyConfig config = new()
        {
            KeywordDeclarations = KeywordDeclarations,
            Keywords = Keywords,
            KeywordConstants = KeywordConstants,
            IncludeDataBrackets = true,
            IncludeColonKeyword = true
        };

        return LispFamilyRules.CreateLexer(config);
    }
}
