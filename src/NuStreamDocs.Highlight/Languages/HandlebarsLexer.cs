// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Handlebars / Mustache template lexer.</summary>
/// <remarks>
/// <c>{{# statement }}</c> / <c>{{ expression }}</c> share the same delimiter
/// pair; the lexer classifies the whole <c>{{ ... }}</c> form as one expression
/// token. <c>{{!-- comment --}}</c> classifies as a comment block.
/// </remarks>
public static class HandlebarsLexer
{
    /// <summary>Gets the singleton Handlebars lexer.</summary>
    public static Lexer Instance { get; } = TemplateFamilyRules.CreateLexer(new()
    {
        StatementOpen = [.. "{{#"u8],
        StatementClose = [.. "}}"u8],
        ExpressionOpen = [.. "{{"u8],
        ExpressionClose = [.. "}}"u8],
        CommentOpen = [.. "{{!"u8],
        CommentClose = [.. "}}"u8]
    });
}
