// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Liquid (Shopify / Jekyll) template lexer.</summary>
/// <remarks>
/// <c>{% statement %}</c>, <c>{{ expression }}</c>; comments are emitted as
/// <c>{% comment %}…{% endcomment %}</c> wrappers, so the dedicated
/// comment-block rule isn't wired here — those classify as the regular
/// statement form.
/// </remarks>
public static class LiquidLexer
{
    /// <summary>Gets the singleton Liquid lexer.</summary>
    public static Lexer Instance { get; } = TemplateFamilyRules.CreateLexer(new()
    {
        StatementOpen = [.. "{%"u8],
        StatementClose = [.. "%}"u8],
        ExpressionOpen = [.. "{{"u8],
        ExpressionClose = [.. "}}"u8],
        CommentOpen = null,
        CommentClose = null
    });
}
