// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight;

/// <summary>
/// Configuration for <see cref="HighlightPlugin"/>.
/// </summary>
/// <param name="ExtraLexers">Caller-supplied lexers registered alongside the built-ins.</param>
/// <param name="WrapInHighlightDiv">Wrap each block in <c>&lt;div class="highlight"&gt;</c> (Pygments / mkdocs-material convention).</param>
/// <param name="EmitTitleBar">When a fence has <c>title="..."</c>, render it as <c>&lt;span class="filename"&gt;</c>; needs <c>WrapInHighlightDiv</c>.</param>
/// <param name="CopyButton">Add a <c>&lt;button class="md-clipboard"&gt;</c> inside the wrapper; theme JS handles the click.</param>
public sealed record HighlightOptions(
    Lexer[] ExtraLexers,
    bool WrapInHighlightDiv,
    bool EmitTitleBar,
    bool CopyButton)
{
    /// <summary>Gets the default option set — built-in lexers, wrapper div + title bar on, copy button off.</summary>
    public static HighlightOptions Default { get; } = new(
        ExtraLexers: [],
        WrapInHighlightDiv: true,
        EmitTitleBar: true,
        CopyButton: false);
}
