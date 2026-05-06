// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.Functional;

/// <summary>Common Lisp lexer.</summary>
/// <remarks>
/// Lisp-family lexer with the Common Lisp <c>def*</c> family (<c>defun</c>,
/// <c>defmacro</c>, <c>defclass</c>, <c>defmethod</c>, <c>defpackage</c>, …) and the
/// <c>nil</c> / <c>t</c> constants. No data brackets — Common Lisp keeps the
/// classical paren-only syntax.
/// </remarks>
public static class CommonLispLexer
{
    /// <summary>Declaration / structure forms.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "defun"u8],
        [.. "defmacro"u8],
        [.. "defclass"u8],
        [.. "defmethod"u8],
        [.. "defgeneric"u8],
        [.. "defstruct"u8],
        [.. "defpackage"u8],
        [.. "defvar"u8],
        [.. "defparameter"u8],
        [.. "defconstant"u8],
        [.. "in-package"u8],
        [.. "lambda"u8]);

    /// <summary>Control-flow / binding forms — shared Lisp-family core plus Common Lisp specifics.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. LispFamilyShared.CommonKeywords,
        [.. "do*"u8],
        [.. "dolist"u8],
        [.. "dotimes"u8],
        [.. "let*"u8],
        [.. "flet"u8],
        [.. "labels"u8],
        [.. "loop"u8],
        [.. "block"u8],
        [.. "return"u8],
        [.. "return-from"u8],
        [.. "go"u8],
        [.. "tagbody"u8],
        [.. "throw"u8],
        [.. "catch"u8],
        [.. "unwind-protect"u8],
        [.. "progn"u8],
        [.. "prog1"u8],
        [.. "prog2"u8],
        [.. "setf"u8],
        [.. "setq"u8]]);

    /// <summary>Constants — <c>nil</c> and <c>t</c> are the canonical Common Lisp booleans.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "nil"u8],
        [.. "t"u8]);

    /// <summary>Gets the singleton Common Lisp lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Common Lisp lexer.</summary>
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
