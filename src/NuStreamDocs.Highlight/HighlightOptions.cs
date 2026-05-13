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
/// <param name="WrapInHighlightDiv">Wrap each block in <c>&lt;div class="highlight"&gt;</c> (mkdocs-material convention).</param>
/// <param name="EmitTitleBar">When a fence has <c>title="..."</c>, render it as <c>&lt;span class="filename"&gt;</c>; needs <c>WrapInHighlightDiv</c>.</param>
/// <param name="CopyButton">Add a <c>&lt;button class="md-clipboard"&gt;</c> inside the wrapper; theme JS handles the click.</param>
/// <param name="AutoDetectLanguage">
/// When true, unlabeled <c>&lt;pre&gt;&lt;code&gt;</c> blocks are scored heuristically;
/// high-confidence matches receive a <c>class="language-X"</c> attribute. Default off.
/// </param>
/// <param name="DetectionLanguages">Optional allow-list of language aliases the auto-detector may select (e.g. <c>"csharp"u8</c>). Empty array means "consider every registered lexer".</param>
public sealed record HighlightOptions(
    in LexerNameValue[] ExtraLexers,
    bool WrapInHighlightDiv,
    bool EmitTitleBar,
    bool CopyButton,
    bool AutoDetectLanguage,
    byte[][] DetectionLanguages)
{
    /// <summary>Gets the default option set — built-in lexers, wrapper div + title bar on, copy button off, auto-detect off.</summary>
    public static HighlightOptions Default { get; } = new(
        [],
        true,
        true,
        false,
        false,
        []);

    /// <summary>Creates a new <see cref="HighlightOptions"/> instance from string-shaped <c>(name, lexer)</c> pairs.</summary>
    /// <param name="extra">Pairs of UTF-16 lexer name and lexer instance.</param>
    /// <returns>A new <see cref="HighlightOptions"/> with the supplied lexers and the default flags.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="extra"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="extra"/> is empty.</exception>
    public static HighlightOptions CreateFromStringLexers(params (string LexerName, Lexer Lexer)[] extra)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extra.Length);
        var values = new LexerNameValue[extra.Length];

        for (var i = 0; i < extra.Length; i++)
        {
            values[i] = new(
                AsciiByteHelpers.ToLowerCaseInvariant(Encoding.UTF8.GetBytes(extra[i].LexerName)),
                extra[i].Lexer);
        }

        return new(values, true, true, false, false, []);
    }
}
