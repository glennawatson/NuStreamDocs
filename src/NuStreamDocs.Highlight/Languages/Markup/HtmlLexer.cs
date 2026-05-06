// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages.Markup;

/// <summary>
/// HTML lexer.
/// </summary>
/// <remarks>
/// HTML and XML use the same tag/attribute/comment/CDATA grammar at
/// the surface; we reuse <see cref="XmlLexer.Instance"/>'s state map
/// under a different language identifier so authors can keep writing
/// <c>```html</c> blocks. Embedded <c>&lt;script&gt;</c> /
/// <c>&lt;style&gt;</c> bodies fall through as plain text for now when
/// the embedded language can't be inferred from the attributes.
/// </remarks>
public static class HtmlLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(XmlLexer.Instance.States);
}
