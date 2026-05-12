// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
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
    private static void AddIf(List<AuditDiagnostic> sink, UrlPath page, AuditOptions options, AuditRule rule, ApiCompatString message, bool fired)
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
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Single pass dispatching on element name; the branching tracks the element vocabulary, not nested logic.")]
    [SuppressMessage(
        "Sonar Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "Single pass dispatching on element name; the branching tracks the element vocabulary, not nested logic.")]
    private static (bool MissingLang, bool MissingTitle, bool MissingViewport, bool MissingH1, bool MultipleH1, bool HeadingSkip) ScanDocument(ReadOnlySpan<byte> html)
    {
        var sawHtml = false;
        var htmlHasLang = false;
        var sawHead = false;
        var titleHasText = false;
        var sawViewport = false;
        var h1Count = 0;
        var anyHeading = false;
        var headingSkip = false;
        var lastLevel = 0;

        HtmlTagCursor cursor = new(html);
        while (cursor.MoveNext())
        {
            if (cursor.IsEndTag)
            {
                continue;
            }

            var name = cursor.Name;
            if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "html"u8))
            {
                sawHtml = true;
                htmlHasLang = AuditText.HasNonEmptyAttribute(cursor.Attributes, "lang"u8);
            }
            else if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "head"u8))
            {
                sawHead = true;
            }
            else if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "title"u8))
            {
                titleHasText |= AuditText.HasText(cursor.RawText);
            }
            else if (AsciiByteHelpers.EqualsIgnoreAsciiCase(name, "meta"u8))
            {
                sawViewport |= IsViewportMeta(cursor);
            }
            else
            {
                var level = HeadingLevel(name);
                if (level != 0)
                {
                    anyHeading = true;
                    h1Count += level == 1 ? 1 : 0;
                    headingSkip |= lastLevel != 0 && level > lastLevel + 1;
                    lastLevel = level;
                }
            }
        }

        return (sawHtml && !htmlHasLang, sawHead && !titleHasText, sawHead && !sawViewport, anyHeading && h1Count == 0, h1Count > 1, headingSkip);
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
        if (name.Length != 2 || AsciiByteHelpers.ToAsciiLowerByte(name[0]) != (byte)'h' || !AsciiByteHelpers.IsAsciiDigit(name[1]))
        {
            return 0;
        }

        var digit = name[1] - (byte)'0';
        return digit is >= 1 and <= MaxHeadingLevel ? digit : 0;
    }
}
