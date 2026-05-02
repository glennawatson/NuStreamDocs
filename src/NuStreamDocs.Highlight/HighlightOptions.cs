// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Configuration for <see cref="HighlightPlugin"/>.
/// </summary>
/// <param name="ExtraLexers">Caller-supplied lexers registered alongside the built-ins.</param>
/// <param name="WrapInHighlightDiv">Wrap each block in <c>&lt;div class="highlight"&gt;</c> (Pygments / mkdocs-material convention).</param>
/// <param name="EmitTitleBar">When a fence has <c>title="..."</c>, render it as <c>&lt;span class="filename"&gt;</c>; needs <c>WrapInHighlightDiv</c>.</param>
/// <param name="CopyButton">Add a <c>&lt;button class="md-clipboard"&gt;</c> inside the wrapper; theme JS handles the click.</param>
public sealed record HighlightOptions(
    in LexerNameValue[] ExtraLexers,
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

    /// <summary>Creates a new <see cref="HighlightOptions"/> instance from string-shaped <c>(name, lexer)</c> pairs.</summary>
    /// <param name="extra">Pairs of UTF-16 lexer name and lexer instance.</param>
    /// <returns>A new <see cref="HighlightOptions"/> with the supplied lexers and the default flags.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="extra"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="extra"/> is empty.</exception>
    /// <remarks>Encodes each name to UTF-8 once and ASCII-lowercases it so the per-block lookup stays byte-only.</remarks>
    public static HighlightOptions CreateFromStringLexers(params (string LexerName, Lexer Lexer)[] extra)
    {
        ArgumentNullException.ThrowIfNull(extra);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extra.Length);
        var values = new LexerNameValue[extra.Length];

        for (var i = 0; i < extra.Length; i++)
        {
            values[i] = new LexerNameValue(
                AsciiByteHelpers.ToLowerCaseInvariant(Encoding.UTF8.GetBytes(extra[i].LexerName)),
                extra[i].Lexer);
        }

        return new(values, true, true, false);
    }
}
