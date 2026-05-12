// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Audit;

/// <summary>Identifies one accessibility or performance lint the audit can raise.</summary>
public enum AuditRule
{
    /// <summary>An <c>&lt;img&gt;</c> has no <c>alt</c> attribute. (An explicit empty <c>alt=""</c> is allowed and marks the image decorative.)</summary>
    ImageMissingAlt,

    /// <summary>An <c>&lt;img&gt;</c> declares neither both <c>width</c> and <c>height</c> nor an <c>aspect-ratio</c> style, so the browser cannot reserve space for it (layout shift).</summary>
    ImageMissingDimensions,

    /// <summary>The heading outline skips a level (for example an <c>&lt;h1&gt;</c> followed directly by an <c>&lt;h3&gt;</c>).</summary>
    HeadingLevelSkipped,

    /// <summary>The page has headings but no <c>&lt;h1&gt;</c>.</summary>
    HeadingMissingH1,

    /// <summary>The page has more than one <c>&lt;h1&gt;</c>.</summary>
    HeadingMultipleH1,

    /// <summary>The <c>&lt;html&gt;</c> element has no <c>lang</c> attribute.</summary>
    HtmlMissingLang,

    /// <summary>The document has no non-empty <c>&lt;title&gt;</c>.</summary>
    DocumentMissingTitle,

    /// <summary>The document has no <c>&lt;meta name="viewport"&gt;</c>.</summary>
    DocumentMissingViewport,

    /// <summary>An <c>&lt;a&gt;</c> element has no discernible text and no accessible-name attribute.</summary>
    EmptyLink,

    /// <summary>A <c>&lt;button&gt;</c> element has no discernible text and no accessible-name attribute.</summary>
    EmptyButton,

    /// <summary>An element declares a positive <c>tabindex</c>, which disrupts the natural focus order.</summary>
    PositiveTabIndex,

    /// <summary>An <c>&lt;input&gt;</c>, <c>&lt;select&gt;</c>, or <c>&lt;textarea&gt;</c> has no associated label or accessible-name attribute.</summary>
    UnlabeledFormControl,

    /// <summary>A <c>&lt;script src&gt;</c> in <c>&lt;head&gt;</c> is render-blocking (no <c>async</c>, <c>defer</c>, or <c>type="module"</c>).</summary>
    RenderBlockingScript
}
