// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>ERB (Embedded Ruby) template lexer.</summary>
/// <remarks>
/// <c>&lt;% statement %&gt;</c>, <c>&lt;%= expression %&gt;</c>, <c>&lt;%# comment %&gt;</c>.
/// The expression form's <c>&lt;%=</c> opener is recognized via the longer-prefix
/// rule order — the comment opener is checked first, then the expression form,
/// then the bare statement form.
/// </remarks>
public static class ErbLexer
{
    /// <summary>Gets the singleton ERB lexer.</summary>
    public static Lexer Instance { get; } = TemplateFamilyRules.CreateLexer(new()
    {
        StatementOpen = [.. "<%"u8],
        StatementClose = [.. "%>"u8],
        ExpressionOpen = [.. "<%="u8],
        ExpressionClose = [.. "%>"u8],
        CommentOpen = [.. "<%#"u8],
        CommentClose = [.. "%>"u8]
    });
}
