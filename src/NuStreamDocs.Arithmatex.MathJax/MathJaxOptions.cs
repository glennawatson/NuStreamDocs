// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Arithmatex.MathJax;

/// <summary>Configuration for <see cref="MathJaxPlugin"/>.</summary>
/// <remarks>
/// MathJax 3 reads <c>window.MathJax</c> at startup; the plugin emits a small inline
/// configuration object before the loader script so the runtime picks up our processing
/// class hooks. The CDN URL points at the most common <c>tex-mml-chtml</c> bundle by
/// default; sites that need a different output (SVG, PreView only, etc.) can override.
/// </remarks>
/// <param name="LoaderUrl">URL of the MathJax loader script. Default is the jsDelivr-hosted <c>tex-mml-chtml</c> bundle for MathJax 3.</param>
/// <param name="ProcessHtmlClass">
/// Regex (matched as a whole-word selector) of HTML <c>class</c> values MathJax should typeset. Default
/// <c>arithmatex</c> matches what <c>NuStreamDocs.Arithmatex</c> emits.
/// </param>
/// <param name="IgnoreHtmlClass">
/// Regex of HTML <c>class</c> values MathJax should skip. Default <c>.*|</c> means "ignore everything by
/// default" so we only typeset the explicitly-tagged blocks.
/// </param>
public readonly record struct MathJaxOptions(
    UrlPath LoaderUrl,
    ApiCompatString ProcessHtmlClass,
    ApiCompatString IgnoreHtmlClass)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static MathJaxOptions Default { get; } = new(
        "https://cdn.jsdelivr.net/npm/mathjax@3/es5/tex-mml-chtml.js",
        "arithmatex",
        ".*|");
}
