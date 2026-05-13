// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>Document-level structure lints: <c>&lt;html lang&gt;</c>, <c>&lt;title&gt;</c>, <c>&lt;meta viewport&gt;</c>, and the heading outline.</summary>
internal static class DocumentStructureAudit
{
    /// <summary>Highest legal HTML heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>Diagnostic message for a missing <c>lang</c> attribute on <c>&lt;html&gt;</c>.</summary>
    private const string MissingLangMessage =
        "<html> has no lang attribute; assistive technology cannot determine the page language.";

    /// <summary>Diagnostic message for a missing or empty document title.</summary>
    private const string MissingTitleMessage =
        "Document has no non-empty <title>.";

    /// <summary>Diagnostic message for a missing responsive-viewport meta tag.</summary>
    private const string MissingViewportMessage =
        "Document has no <meta name=\"viewport\">; the page will not adapt to small screens.";

    /// <summary>Diagnostic message for a heading outline that skips a level.</summary>
    private const string HeadingSkipMessage =
        "Heading outline skips a level (for example an <h1> followed directly by an <h3>).";

    /// <summary>Diagnostic message for a page that has headings but no <c>&lt;h1&gt;</c>.</summary>
    private const string MissingH1Message =
        "Page has headings but no <h1>.";

    /// <summary>Diagnostic message for a page with more than one <c>&lt;h1&gt;</c>.</summary>
    private const string MultipleH1Message =
        "Page has more than one <h1>.";

    /// <summary>Runs the document-structure lints over one page.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="page">Site-relative URL of the page.</param>
    /// <param name="options">Audit options (rule toggles).</param>
    /// <param name="sink">Receives the findings.</param>
    public static void Check(ReadOnlySpan<byte> html, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        var (missingLang, missingTitle, missingViewport, missingH1, multipleH1, headingSkip) = ScanDocument(html);
        AddIf(sink, page, options, AuditRule.HtmlMissingLang, MissingLangMessage, missingLang);
        AddIf(sink, page, options, AuditRule.DocumentMissingTitle, MissingTitleMessage, missingTitle);
        AddIf(sink, page, options, AuditRule.DocumentMissingViewport, MissingViewportMessage, missingViewport);
        AddIf(sink, page, options, AuditRule.HeadingMissingH1, MissingH1Message, missingH1);
        AddIf(sink, page, options, AuditRule.HeadingMultipleH1, MultipleH1Message, multipleH1);
        AddIf(sink, page, options, AuditRule.HeadingLevelSkipped, HeadingSkipMessage, headingSkip);
    }

    /// <summary>Adds a finding when both the lint is enabled and the condition fired.</summary>
    /// <param name="sink">Receives the finding.</param>
    /// <param name="page">Page URL.</param>
    /// <param name="options">Audit options.</param>
    /// <param name="rule">The lint.</param>
    /// <param name="message">Diagnostic message.</param>
    /// <param name="fired">Whether the condition holds for this page.</param>
    private static void AddIf(
        List<AuditDiagnostic> sink,
        UrlPath page,
        AuditOptions options,
        AuditRule rule,
        ApiCompatString message,
        bool fired)
    {
        if (!fired || !options.IsRuleEnabled(rule))
        {
            return;
        }

        sink.Add(new(page, rule, message));
    }

    /// <summary>Walks the page once and derives the document-structure conditions the lints test.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <returns>Tuple of (missing lang, missing title, missing viewport, missing h1, multiple h1, heading skip).</returns>
    private static (bool MissingLang, bool MissingTitle, bool MissingViewport, bool MissingH1, bool MultipleH1, bool
        HeadingSkip) ScanDocument(ReadOnlySpan<byte> html)
    {
        DocScanState state = default;
        HtmlTagCursor cursor = new(html);
        while (cursor.MoveNext())
        {
            if (!cursor.IsEndTag)
            {
                ApplyTag(ref state, cursor);
            }
        }

        return (
            state.SawHtml && !state.HtmlHasLang,
            state.SawHead && !state.TitleHasText,
            state.SawHead && !state.SawViewport,
            state.AnyHeading && state.H1Count == 0,
            state.H1Count > 1,
            state.HeadingSkip);
    }

    /// <summary>Folds one start tag into the running scan state.</summary>
    /// <param name="state">Running scan state.</param>
    /// <param name="cursor">Cursor positioned on the start tag.</param>
    private static void ApplyTag(ref DocScanState state, HtmlTagCursor cursor)
    {
        var name = cursor.Name;
        if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "html"u8))
        {
            state.SawHtml = true;
            state.HtmlHasLang = AuditText.HasNonEmptyAttribute(cursor.Attributes, "lang"u8);
            return;
        }

        if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "head"u8))
        {
            state.SawHead = true;
            return;
        }

        if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "title"u8))
        {
            state.TitleHasText |= AuditText.HasText(cursor.RawText);
            return;
        }

        if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "meta"u8))
        {
            state.SawViewport |= IsViewportMeta(cursor);
            return;
        }

        ApplyHeading(ref state, name);
    }

    /// <summary>Folds an <c>h1</c>-<c>h6</c> tag into the heading-outline portion of the scan state.</summary>
    /// <param name="state">Running scan state.</param>
    /// <param name="name">Tag name bytes.</param>
    private static void ApplyHeading(ref DocScanState state, ReadOnlySpan<byte> name)
    {
        var level = HeadingLevel(name);
        if (level == 0)
        {
            return;
        }

        state.AnyHeading = true;
        state.H1Count += level == 1 ? 1 : 0;
        state.HeadingSkip |= state.LastLevel != 0 && level > state.LastLevel + 1;
        state.LastLevel = level;
    }

    /// <summary>True when a <c>&lt;meta&gt;</c> tag declares <c>name="viewport"</c>.</summary>
    /// <param name="cursor">Cursor positioned on the <c>&lt;meta&gt;</c> tag.</param>
    /// <returns><see langword="true"/> for the responsive-viewport meta.</returns>
    private static bool IsViewportMeta(HtmlTagCursor cursor) =>
        cursor.TryGetAttribute("name"u8, out var metaName)
        && AsciiByteHelpers.EqualsIgnoreAsciiCase(AsciiByteHelpers.TrimAsciiWhitespace(metaName), "viewport"u8);

    /// <summary>Returns the heading level (1-6) for an <c>h1</c>-<c>h6</c> tag name, or 0 for anything else.</summary>
    /// <param name="name">Tag name bytes.</param>
    /// <returns>The heading level, or 0.</returns>
    private static int HeadingLevel(ReadOnlySpan<byte> name)
    {
        if (name.Length != 2 || AsciiByteHelpers.ToAsciiLowerByte(name[0]) != (byte)'h' ||
            !AsciiByteHelpers.IsAsciiDigit(name[1]))
        {
            return 0;
        }

        var digit = name[1] - (byte)'0';
        return digit is >= 1 and <= MaxHeadingLevel ? digit : 0;
    }

    /// <summary>Mutable accumulator threaded through the single document scan.</summary>
    /// <param name="SawHtml">Whether an <c>&lt;html&gt;</c> tag was seen.</param>
    /// <param name="HtmlHasLang">Whether the <c>&lt;html&gt;</c> tag carried a non-empty <c>lang</c> attribute.</param>
    /// <param name="SawHead">Whether a <c>&lt;head&gt;</c> tag was seen.</param>
    /// <param name="TitleHasText">Whether a <c>&lt;title&gt;</c> with non-whitespace text was seen.</param>
    /// <param name="SawViewport">Whether a responsive-viewport <c>&lt;meta&gt;</c> was seen.</param>
    /// <param name="H1Count">Count of <c>&lt;h1&gt;</c> tags seen.</param>
    /// <param name="AnyHeading">Whether any heading tag was seen.</param>
    /// <param name="HeadingSkip">Whether the heading outline skipped a level.</param>
    /// <param name="LastLevel">Level of the most recently seen heading (0 before the first).</param>
    private record struct DocScanState(
        bool SawHtml,
        bool HtmlHasLang,
        bool SawHead,
        bool TitleHasText,
        bool SawViewport,
        int H1Count,
        bool AnyHeading,
        bool HeadingSkip,
        int LastLevel);
}
