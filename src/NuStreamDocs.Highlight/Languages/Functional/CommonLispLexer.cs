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
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "defun defmacro defclass defmethod defgeneric defstruct defpackage defvar defparameter defconstant in-package lambda"u8);

    /// <summary>Control-flow / binding forms — shared Lisp-family core plus Common Lisp specifics.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        LispFamilyShared.CommonKeywordsLiteral,
        "do* dolist dotimes let* flet labels loop block return return-from go tagbody throw catch unwind-protect progn prog1 prog2 setf setq"u8);

    /// <summary>Constants — <c>nil</c> and <c>t</c> are the canonical Common Lisp booleans.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated("nil t"u8);

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
