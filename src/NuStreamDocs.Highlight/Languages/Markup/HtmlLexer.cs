// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages.Markup;

/// <summary>HTML lexer (shares the XML grammar). Embedded <c>&lt;script&gt;</c> / <c>&lt;style&gt;</c> bodies pass through as plain text.</summary>
public static class HtmlLexer
{
    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(XmlLexer.Instance.States);
}
