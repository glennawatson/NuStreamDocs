// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Highlight;

/// <summary>The Pygments-default stylesheet bytes that style the token-class spans <see cref="HighlightEmitter"/> emits.</summary>
/// <remarks>
/// Mirrors the upstream <c>pymdownx.highlight</c> default colors so authors get a familiar look without
/// the theme having to bundle Pygments. Both light and dark schemes are scoped under the
/// <c>.highlight</c> wrapper and the dark scheme is gated on <c>[data-md-color-scheme="slate"]</c>
/// so the active theme's color toggle picks the right palette automatically.
/// </remarks>
public static class HighlightStylesheet
{
    /// <summary>Gets the site-relative path the stylesheet is written to.</summary>
    public static FilePath AssetPath => new("assets/stylesheets/highlight.css");

    /// <summary>Gets the UTF-8 absolute URL bytes (<c>/</c>-prefixed) for the stylesheet, suitable for an HTML <c>href</c> emitted directly into the page <c>&lt;head&gt;</c>.</summary>
    public static byte[] AssetHref { get; } = [.. "/assets/stylesheets/highlight.css"u8];

    /// <summary>Gets the UTF-8 stylesheet source as a span; <see cref="GetBytes"/> copies it per asset request.</summary>
    private static ReadOnlySpan<byte> CssBytes =>
        """
        /*
         * NuStreamDocs.Highlight default stylesheet.
         * Pygments-compatible token classes for the spans emitted by HighlightEmitter.
         * Light scheme is the default; the dark scheme overrides apply when the page
         * carries data-md-color-scheme="slate" (matches mkdocs-material's Slate theme).
         */
        .highlight pre {
          line-height: 1.45;
          margin: 0;
          padding: 0.75rem 1rem;
          overflow-x: auto;
        }

        .highlight code {
          background: transparent;
          padding: 0;
          font-family: inherit;
        }

        .highlight {
          background: #f6f8fa;
          border-radius: 4px;
        }

        /* Comments */
        .highlight .c, .highlight .c1, .highlight .cm, .highlight .cs, .highlight .cp { color: #6a737d; font-style: italic; }

        /* Keywords */
        .highlight .k, .highlight .kc, .highlight .kd, .highlight .kt { color: #d73a49; font-weight: 600; }

        /* Names */
        .highlight .n  { color: #24292e; }
        .highlight .nf { color: #6f42c1; font-weight: 600; }
        .highlight .nc { color: #6f42c1; font-weight: 600; }
        .highlight .nb { color: #005cc5; }
        .highlight .na { color: #22863a; }

        /* Strings */
        .highlight .s, .highlight .s1, .highlight .s2 { color: #032f62; }
        .highlight .se { color: #d73a49; }

        /* Numbers */
        .highlight .mi, .highlight .mf, .highlight .mh { color: #005cc5; }

        /* Operators / punctuation */
        .highlight .o { color: #d73a49; }
        .highlight .p { color: #24292e; }

        /* Whitespace stays invisible */
        .highlight .w { color: inherit; }

        /* Diff blocks */
        .highlight .gi { background: #e6ffec; color: #22863a; }
        .highlight .gd { background: #ffeef0; color: #b31d28; }
        .highlight .gh { color: #6a737d; font-weight: 600; }
        .highlight .gu { color: #6a737d; }

        /* Slate / dark-mode palette */
        [data-md-color-scheme="slate"] .highlight { background: #161b22; }
        [data-md-color-scheme="slate"] .highlight .c,
        [data-md-color-scheme="slate"] .highlight .c1,
        [data-md-color-scheme="slate"] .highlight .cm,
        [data-md-color-scheme="slate"] .highlight .cs,
        [data-md-color-scheme="slate"] .highlight .cp { color: #8b949e; }
        [data-md-color-scheme="slate"] .highlight .k,
        [data-md-color-scheme="slate"] .highlight .kc,
        [data-md-color-scheme="slate"] .highlight .kd,
        [data-md-color-scheme="slate"] .highlight .kt { color: #ff7b72; }
        [data-md-color-scheme="slate"] .highlight .n  { color: #c9d1d9; }
        [data-md-color-scheme="slate"] .highlight .nf,
        [data-md-color-scheme="slate"] .highlight .nc { color: #d2a8ff; }
        [data-md-color-scheme="slate"] .highlight .nb { color: #79c0ff; }
        [data-md-color-scheme="slate"] .highlight .na { color: #7ee787; }
        [data-md-color-scheme="slate"] .highlight .s,
        [data-md-color-scheme="slate"] .highlight .s1,
        [data-md-color-scheme="slate"] .highlight .s2 { color: #a5d6ff; }
        [data-md-color-scheme="slate"] .highlight .se { color: #ff7b72; }
        [data-md-color-scheme="slate"] .highlight .mi,
        [data-md-color-scheme="slate"] .highlight .mf,
        [data-md-color-scheme="slate"] .highlight .mh { color: #79c0ff; }
        [data-md-color-scheme="slate"] .highlight .o  { color: #ff7b72; }
        [data-md-color-scheme="slate"] .highlight .p  { color: #c9d1d9; }
        [data-md-color-scheme="slate"] .highlight .gi { background: #033a16; color: #7ee787; }
        [data-md-color-scheme="slate"] .highlight .gd { background: #67060c; color: #ffa198; }
        [data-md-color-scheme="slate"] .highlight .gh,
        [data-md-color-scheme="slate"] .highlight .gu { color: #8b949e; }
        """u8;

    /// <summary>Returns a fresh copy of the stylesheet bytes; the asset registry takes ownership.</summary>
    /// <returns>UTF-8 stylesheet bytes.</returns>
    public static byte[] GetBytes() => [.. CssBytes];
}
