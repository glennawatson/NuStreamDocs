// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Per-language configuration consumed by <see cref="TemplateFamilyRules.Build"/>.</summary>
/// <remarks>
/// Templating engines share a delimited shape: HTML / text outside delimiters,
/// statement / expression / comment markers inside. The helper recognizes each
/// marked block as a single token (statement = keyword, expression = name,
/// comment = comment-multi); themes get the visual hook even though the inner
/// expression isn't recursively classified. Dialects vary on the delimiter pairs
/// (<c>{% %}</c> for Jinja / Twig, <c>&lt;% %&gt;</c> for ERB, <c>{{ }}</c> for
/// Handlebars / Mustache, <c>${ }</c> for Velocity).
/// </remarks>
internal readonly record struct TemplateFamilyConfig
{
    /// <summary>Gets the statement-block opener (e.g. <c>"{%"u8</c> for Jinja, <c>"&lt;%"u8</c> for ERB).</summary>
    public byte[] StatementOpen { get; init; }

    /// <summary>Gets the statement-block closer (e.g. <c>"%}"u8</c>, <c>"%&gt;"u8</c>).</summary>
    public byte[] StatementClose { get; init; }

    /// <summary>Gets the expression-block opener (e.g. <c>"{{"u8</c>, <c>"&lt;%="u8</c>, <c>"${"u8</c>).</summary>
    public byte[] ExpressionOpen { get; init; }

    /// <summary>Gets the expression-block closer (e.g. <c>"}}"u8</c>, <c>"%&gt;"u8</c>, <c>"}"u8</c>).</summary>
    public byte[] ExpressionClose { get; init; }

    /// <summary>Gets the optional comment-block opener (e.g. <c>"{#"u8</c> for Jinja, <c>"{{!"u8</c> for Handlebars); <see langword="null"/> disables the rule.</summary>
    public byte[]? CommentOpen { get; init; }

    /// <summary>Gets the optional comment-block closer (e.g. <c>"#}"u8</c>, <c>"}}"u8</c>).</summary>
    public byte[]? CommentClose { get; init; }
}
