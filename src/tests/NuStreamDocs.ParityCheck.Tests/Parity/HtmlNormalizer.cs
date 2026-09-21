// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Canonicalizes HTML so that insignificant differences do not count as parity differences.</summary>
internal static class HtmlNormalizer
{
    /// <summary>Tags around which whitespace is insignificant.</summary>
    private static readonly FrozenSet<string> Boundary = FrozenSet.ToFrozenSet(
        [
            "p", "ul", "ol", "li", "blockquote", "pre", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "br",
            "div", "table", "thead", "tbody", "tr", "th", "td", "dl", "dt", "dd", "section", "details", "summary",
        ],
        StringComparer.Ordinal);

    /// <summary>Returns the HTML as written with the out-of-scope styling markup (highlighter wrappers, line anchors, permalinks) removed.</summary>
    /// <param name="html">HTML to clean.</param>
    /// <returns>The HTML without styling markup.</returns>
    internal static string StripMarkup(string html)
    {
        var tokens = Strip(html);
        var builder = new StringBuilder(html.Length);
        for (var i = 0; i < tokens.Count; i++)
        {
            _ = builder.Append(tokens[i].Raw);
        }

        return builder.ToString();
    }

    /// <summary>Canonicalizes HTML for comparison.</summary>
    /// <param name="html">HTML to canonicalize.</param>
    /// <returns>The canonical form: equal for HTML that differs only in insignificant ways.</returns>
    internal static string Normalize(string html)
    {
        var tokens = Strip(html);
        var builder = new StringBuilder(html.Length);
        var pre = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.IsTag)
            {
                pre = token.Name == "pre" ? Math.Max(0, pre + (token.Closing ? -1 : 1)) : pre;
                _ = builder.Append(token.Text);
                continue;
            }

            _ = builder.Append(pre == 0 ? NormalizeText(tokens, i) : token.Text);
        }

        return builder.ToString();
    }

    /// <summary>Tokenizes HTML and removes the styling markup.</summary>
    /// <param name="html">HTML to process.</param>
    /// <returns>The remaining tokens.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<HtmlToken> Strip(string html) =>
        HtmlStyleStripper.Strip(HtmlTokenizer.Tokenize(html.Replace("\r\n", "\n", StringComparison.Ordinal)));

    /// <summary>Collapses the whitespace of a text token and trims it next to block-level tags.</summary>
    /// <param name="tokens">All tokens.</param>
    /// <param name="index">Index of the text token.</param>
    /// <returns>The normalized text.</returns>
    private static string NormalizeText(List<HtmlToken> tokens, int index)
    {
        var text = HtmlText.CollapseWhitespace(tokens[index].Text);
        if (index == 0 || IsBoundary(tokens[index - 1]))
        {
            text = text.TrimStart();
        }

        if (index == tokens.Count - 1 || IsBoundary(tokens[index + 1]))
        {
            text = text.TrimEnd();
        }

        return text;
    }

    /// <summary>Determines whether whitespace next to the token is insignificant.</summary>
    /// <param name="token">Token to test.</param>
    /// <returns><see langword="true"/> for block-level tags.</returns>
    private static bool IsBoundary(HtmlToken token) => token.IsTag && Boundary.Contains(token.Name);
}
