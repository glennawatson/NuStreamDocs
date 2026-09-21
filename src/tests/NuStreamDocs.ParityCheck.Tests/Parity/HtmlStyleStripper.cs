// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>
/// Removes CSS-only and highlighter/toc markup: class and style attributes, attribute-less spans (syntax-highlighter
/// tokens), highlight wrapper divs, permalink anchors, code line spans and anchors. These never count as parity differences.
/// </summary>
[DebuggerDisplay("{Result.Count} tokens kept")]
internal sealed class HtmlStyleStripper
{
    /// <summary>Class fragment that marks a permalink anchor.</summary>
    private const string PermalinkClass = "headerlink";

    /// <summary>Id prefix that marks a code line anchor.</summary>
    private const string CodeLineAnchorPrefix = "__codelineno";

    /// <summary>Id prefix that marks a generated code line span.</summary>
    private const string CodeLineSpanPrefix = "__span";

    /// <summary>Class fragment that marks a highlighter wrapper div.</summary>
    private const string HighlightClass = "highlight";

    /// <summary>The <c>span</c> and <c>div</c> elements that are open, innermost last.</summary>
    private readonly List<OpenElement> _open = [];

    /// <summary>Nesting depth inside a permalink or code line anchor being skipped.</summary>
    private int _skippedAnchorDepth;

    /// <summary>Initializes a new instance of the <see cref="HtmlStyleStripper"/> class.</summary>
    /// <param name="capacity">Number of tokens expected.</param>
    private HtmlStyleStripper(int capacity) => Result = [with(capacity)];

    /// <summary>Gets the tokens that remain.</summary>
    internal List<HtmlToken> Result { get; }

    /// <summary>Removes styling markup from a token list.</summary>
    /// <param name="tokens">Tokens of the document.</param>
    /// <returns>The tokens that remain.</returns>
    internal static List<HtmlToken> Strip(List<HtmlToken> tokens)
    {
        var stripper = new HtmlStyleStripper(tokens.Count);
        for (var i = 0; i < tokens.Count; i++)
        {
            stripper.Accept(tokens[i]);
        }

        return stripper.Result;
    }

    /// <summary>Determines whether the token opens an anchor that is removed together with its content.</summary>
    /// <param name="token">Token to test.</param>
    /// <returns><see langword="true"/> for permalink anchors and code line anchors.</returns>
    private static bool OpensSkippedAnchor(HtmlToken token) =>
        token is { IsTag: true, Closing: false, Name: "a" }
        && (token.ClassValue.Contains(PermalinkClass, StringComparison.Ordinal) || token.IdValue.StartsWith(CodeLineAnchorPrefix, StringComparison.Ordinal));

    /// <summary>Determines whether the token is a <c>span</c> or <c>div</c> tag that is not self-closing.</summary>
    /// <param name="token">Token to test.</param>
    /// <returns><see langword="true"/> when the token may be elided.</returns>
    private static bool IsElidable(HtmlToken token) =>
        token is { IsTag: true, Name: "span" or "div" } && !token.Text.EndsWith("/>", StringComparison.Ordinal);

    /// <summary>Determines whether an opening <c>span</c> or <c>div</c> tag is dropped.</summary>
    /// <param name="token">Opening tag.</param>
    /// <returns><see langword="true"/> for attribute-less spans, code line spans and highlighter wrapper divs.</returns>
    private static bool ShouldElide(HtmlToken token) =>
        token.Name == "span"
            ? !token.HasAttributes || token.IdValue.StartsWith(CodeLineSpanPrefix, StringComparison.Ordinal)
            : token.ClassValue.Contains(HighlightClass, StringComparison.Ordinal);

    /// <summary>Processes one token.</summary>
    /// <param name="token">Token to keep or drop.</param>
    private void Accept(HtmlToken token)
    {
        if (_skippedAnchorDepth > 0)
        {
            SkipAnchorToken(token);
            return;
        }

        if (OpensSkippedAnchor(token))
        {
            _skippedAnchorDepth = 1;
            return;
        }

        if (!IsElidable(token))
        {
            Result.Add(token);
            return;
        }

        if (token.Closing)
        {
            CloseElement(token);
            return;
        }

        var elide = ShouldElide(token);
        _open.Add(new(token.Name, elide));
        if (!elide)
        {
            Result.Add(token);
        }
    }

    /// <summary>Tracks the anchor nesting depth while an anchor is being skipped.</summary>
    /// <param name="token">Token inside the skipped anchor.</param>
    private void SkipAnchorToken(HtmlToken token)
    {
        if (token is { IsTag: true, Name: "a" })
        {
            _skippedAnchorDepth += token.Closing ? -1 : 1;
        }
    }

    /// <summary>Closes the innermost open element with the token's name, keeping the closing tag only when its opening tag was kept.</summary>
    /// <param name="token">Closing tag.</param>
    private void CloseElement(HtmlToken token)
    {
        var index = _open.FindLastIndex(element => element.Name == token.Name);
        var elided = index >= 0 && _open[index].Elided;
        if (index >= 0)
        {
            _open.RemoveRange(index, _open.Count - index);
        }

        if (!elided)
        {
            Result.Add(token);
        }
    }

    /// <summary>A <c>span</c> or <c>div</c> that is still open while stripping styling.</summary>
    /// <param name="Name">Tag name.</param>
    /// <param name="Elided">Whether the element's tags are dropped.</param>
    [DebuggerDisplay("{Name} elided={Elided}")]
    private sealed record OpenElement(string Name, bool Elided);
}
