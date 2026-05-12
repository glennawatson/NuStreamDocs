// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>Lints over <c>&lt;img&gt;</c> elements: missing <c>alt</c> text and missing intrinsic dimensions.</summary>
internal static class ImageAudit
{
    /// <summary>Diagnostic message for an image with no <c>alt</c> attribute.</summary>
    private const string MissingAltMessage =
        "<img> has no alt attribute. Add descriptive alt text, or alt=\"\" if the image is decorative.";

    /// <summary>Diagnostic message for an image that gives the browser no way to reserve layout space.</summary>
    private const string MissingDimensionsMessage =
        "<img> declares neither both width and height nor an aspect-ratio style; the page will shift as it loads.";

    /// <summary>Runs the image lints over one page.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="page">Site-relative URL of the page.</param>
    /// <param name="options">Audit options (rule toggles).</param>
    /// <param name="sink">Receives the findings.</param>
    public static void Check(ReadOnlySpan<byte> html, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        var checkAlt = options.IsRuleEnabled(AuditRule.ImageMissingAlt);
        var checkDimensions = options.IsRuleEnabled(AuditRule.ImageMissingDimensions);
        if (!checkAlt && !checkDimensions)
        {
            return;
        }

        // Quick pre-check: a page with no <img> needs no tag scan at all.
        if (html.IndexOf("<img"u8) < 0)
        {
            return;
        }

        HtmlTagCursor cursor = new(html);
        while (cursor.MoveNext())
        {
            if (cursor.IsEndTag || !AsciiByteHelpers.EqualsIgnoreAsciiCase(cursor.Name, "img"u8))
            {
                continue;
            }

            CheckImage(cursor.Attributes, page, checkAlt, checkDimensions, sink);
        }
    }

    /// <summary>Emits the alt / dimension findings for one <c>&lt;img&gt;</c>.</summary>
    /// <param name="attributes">Attribute text from the tag.</param>
    /// <param name="page">Page URL.</param>
    /// <param name="checkAlt">Whether the missing-alt lint is enabled.</param>
    /// <param name="checkDimensions">Whether the missing-dimensions lint is enabled.</param>
    /// <param name="sink">Receives findings.</param>
    private static void CheckImage(ReadOnlySpan<byte> attributes, UrlPath page, bool checkAlt, bool checkDimensions, List<AuditDiagnostic> sink)
    {
        if (checkAlt && !HtmlAttr.Has(attributes, "alt"u8))
        {
            sink.Add(new(page, AuditRule.ImageMissingAlt, MissingAltMessage));
        }

        if (!checkDimensions || HasIntrinsicSize(attributes))
        {
            return;
        }

        sink.Add(new(page, AuditRule.ImageMissingDimensions, MissingDimensionsMessage));
    }

    /// <summary>True when the image declares both <c>width</c> and <c>height</c>, or an inline <c>aspect-ratio</c> style.</summary>
    /// <param name="attributes">Attribute text from the <c>&lt;img&gt;</c> tag.</param>
    /// <returns><see langword="true"/> when the browser can reserve layout space for the image.</returns>
    private static bool HasIntrinsicSize(ReadOnlySpan<byte> attributes)
    {
        if (AuditText.HasNonEmptyAttribute(attributes, "width"u8)
            && AuditText.HasNonEmptyAttribute(attributes, "height"u8))
        {
            return true;
        }

        return HtmlAttr.TryGet(attributes, "style"u8, out var style) && style.IndexOf("aspect-ratio"u8) >= 0;
    }
}
