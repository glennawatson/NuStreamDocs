// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Arithmatex.MathJax;

/// <summary>Construction helpers for the <see cref="MathJaxOptions"/> record.</summary>
public static class MathJaxOptionsExtensions
{
    /// <summary>Extension members for <c>MathJaxOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in MathJaxOptions options)
    {
        /// <summary>Replaces the MathJax loader URL.</summary>
        /// <param name="loaderUrl">New loader URL.</param>
        /// <returns>The updated options.</returns>
        public MathJaxOptions WithLoaderUrl(in UrlPath loaderUrl) =>
            options with { LoaderUrl = loaderUrl };

        /// <summary>Replaces the <c>processHtmlClass</c> regex.</summary>
        /// <param name="processHtmlClass">Regex of HTML class values MathJax should typeset.</param>
        /// <returns>The updated options.</returns>
        public MathJaxOptions WithProcessHtmlClass(
            in ApiCompatString processHtmlClass) =>
            options with { ProcessHtmlClass = processHtmlClass };

        /// <summary>Replaces the <c>ignoreHtmlClass</c> regex.</summary>
        /// <param name="ignoreHtmlClass">Regex of HTML class values MathJax should skip.</param>
        /// <returns>The updated options.</returns>
        public MathJaxOptions
            WithIgnoreHtmlClass(in ApiCompatString ignoreHtmlClass) =>
            options with { IgnoreHtmlClass = ignoreHtmlClass };
    }
}
