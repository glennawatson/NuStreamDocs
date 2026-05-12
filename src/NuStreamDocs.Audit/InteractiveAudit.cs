// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>Accessibility lints over interactive elements: empty links and buttons, positive <c>tabindex</c>, and unlabeled form controls.</summary>
internal static class InteractiveAudit
{
    /// <summary>Diagnostic message for a link with no discernible text.</summary>
    private const string EmptyLinkMessage =
        "<a href> has no discernible text and no accessible name (aria-label, title, or a labeled child).";

    /// <summary>Diagnostic message for a button with no discernible text.</summary>
    private const string EmptyButtonMessage =
        "<button> has no discernible text and no accessible name (aria-label, title, or a labeled child).";

    /// <summary>Diagnostic message for an element with a positive <c>tabindex</c>.</summary>
    private const string PositiveTabIndexMessage =
        "Element declares a positive tabindex; use 0 (or omit the attribute) to keep the natural focus order.";

    /// <summary>Diagnostic message for a form control with no associated label.</summary>
    private const string UnlabeledControlMessage =
        "Form control has no associated <label>, aria-label, aria-labelledby, or title.";

    /// <summary>Lowercased <c>&lt;input&gt;</c> types that do not need an associated label.</summary>
    private static readonly byte[][] NonLabelableInputTypes =
    [
        [.. "hidden"u8], [.. "submit"u8], [.. "reset"u8], [.. "button"u8], [.. "image"u8]
    ];

    /// <summary>Runs the interactive-element lints over one page.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="page">Site-relative URL of the page.</param>
    /// <param name="options">Audit options (rule toggles).</param>
    /// <param name="sink">Receives the findings.</param>
    public static void Check(ReadOnlySpan<byte> html, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        if (!AnyRuleEnabled(options))
        {
            return;
        }

        var labelTargets = options.IsRuleEnabled(AuditRule.UnlabeledFormControl) ? CollectLabelTargets(html) : null;
        var labelDepth = 0;

        HtmlTagCursor cursor = new(html);
        while (cursor.MoveNext())
        {
            if (AsciiByteHelpers.EqualsIgnoreAsciiCase(cursor.Name, "label"u8))
            {
                labelDepth = cursor.IsEndTag ? Math.Max(0, labelDepth - 1) : labelDepth + 1;
                continue;
            }

            if (cursor.IsEndTag)
            {
                continue;
            }

            CheckTabIndex(cursor, page, options, sink);
            CheckEmptyLink(html, cursor, page, options, sink);
            CheckEmptyButton(html, cursor, page, options, sink);
            CheckFormControl(cursor, labelDepth, labelTargets, page, options, sink);
        }
    }

    /// <summary>True when any interactive lint is enabled.</summary>
    /// <param name="options">Audit options.</param>
    /// <returns><see langword="true"/> when there is work to do.</returns>
    private static bool AnyRuleEnabled(AuditOptions options) =>
        options.IsRuleEnabled(AuditRule.EmptyLink)
        || options.IsRuleEnabled(AuditRule.EmptyButton)
        || options.IsRuleEnabled(AuditRule.PositiveTabIndex)
        || options.IsRuleEnabled(AuditRule.UnlabeledFormControl);

    /// <summary>Flags a positive <c>tabindex</c> on the current element.</summary>
    /// <param name="cursor">Cursor on the current tag.</param>
    /// <param name="page">Page URL.</param>
    /// <param name="options">Audit options.</param>
    /// <param name="sink">Receives findings.</param>
    private static void CheckTabIndex(HtmlTagCursor cursor, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        if (!options.IsRuleEnabled(AuditRule.PositiveTabIndex) || !HasPositiveTabIndex(cursor.Attributes))
        {
            return;
        }

        sink.Add(new(page, AuditRule.PositiveTabIndex, PositiveTabIndexMessage));
    }

    /// <summary>Flags an <c>&lt;a href&gt;</c> that has no discernible content.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="cursor">Cursor on the current tag.</param>
    /// <param name="page">Page URL.</param>
    /// <param name="options">Audit options.</param>
    /// <param name="sink">Receives findings.</param>
    private static void CheckEmptyLink(ReadOnlySpan<byte> html, HtmlTagCursor cursor, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        if (!options.IsRuleEnabled(AuditRule.EmptyLink) || !AsciiByteHelpers.EqualsIgnoreAsciiCase(cursor.Name, "a"u8))
        {
            return;
        }

        if (!cursor.HasAttribute("href"u8) || !IsEmpty(html, cursor.Attributes, cursor.TagEnd, "a"u8))
        {
            return;
        }

        sink.Add(new(page, AuditRule.EmptyLink, EmptyLinkMessage));
    }

    /// <summary>Flags a <c>&lt;button&gt;</c> that has no discernible content.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="cursor">Cursor on the current tag.</param>
    /// <param name="page">Page URL.</param>
    /// <param name="options">Audit options.</param>
    /// <param name="sink">Receives findings.</param>
    private static void CheckEmptyButton(ReadOnlySpan<byte> html, HtmlTagCursor cursor, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        if (!options.IsRuleEnabled(AuditRule.EmptyButton) || !AsciiByteHelpers.EqualsIgnoreAsciiCase(cursor.Name, "button"u8))
        {
            return;
        }

        if (!IsEmpty(html, cursor.Attributes, cursor.TagEnd, "button"u8))
        {
            return;
        }

        sink.Add(new(page, AuditRule.EmptyButton, EmptyButtonMessage));
    }

    /// <summary>Flags an unlabeled <c>&lt;input&gt;</c>, <c>&lt;select&gt;</c>, or <c>&lt;textarea&gt;</c>.</summary>
    /// <param name="cursor">Cursor on the current tag.</param>
    /// <param name="labelDepth">Open-<c>&lt;label&gt;</c> nesting depth at this point.</param>
    /// <param name="labelTargets">Ids referenced by labels on the page; null when the lint is disabled.</param>
    /// <param name="page">Page URL.</param>
    /// <param name="options">Audit options.</param>
    /// <param name="sink">Receives findings.</param>
    private static void CheckFormControl(HtmlTagCursor cursor, int labelDepth, HashSet<byte[]>? labelTargets, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        if (labelTargets is null || labelDepth != 0 || !options.IsRuleEnabled(AuditRule.UnlabeledFormControl))
        {
            return;
        }

        if (!IsLabelableControl(cursor.Name, cursor.Attributes) || IsControlNamed(cursor.Attributes, labelTargets))
        {
            return;
        }

        sink.Add(new(page, AuditRule.UnlabeledFormControl, UnlabeledControlMessage));
    }

    /// <summary>Collects the non-empty <c>for</c> targets of every <c>&lt;label&gt;</c> on the page.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <returns>The set of referenced control ids, byte-array keyed.</returns>
    private static HashSet<byte[]> CollectLabelTargets(ReadOnlySpan<byte> html)
    {
        HashSet<byte[]> targets = new(ByteArrayComparer.Instance);
        HtmlTagCursor cursor = new(html);
        while (cursor.MoveNext())
        {
            if (cursor.IsEndTag || !AsciiByteHelpers.EqualsIgnoreAsciiCase(cursor.Name, "label"u8))
            {
                continue;
            }

            if (cursor.TryGetAttribute("for"u8, out var target) && AuditText.HasText(target))
            {
                targets.Add([.. AsciiByteHelpers.TrimAsciiWhitespace(target)]);
            }
        }

        return targets;
    }

    /// <summary>True when an <c>&lt;a&gt;</c> or <c>&lt;button&gt;</c> has neither a name attribute nor discernible inner content.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="attributes">The element's attribute text.</param>
    /// <param name="contentStart">Offset of the first byte after the element's start tag.</param>
    /// <param name="tagName">The element's tag name.</param>
    /// <returns><see langword="true"/> when the element is effectively empty.</returns>
    private static bool IsEmpty(ReadOnlySpan<byte> html, ReadOnlySpan<byte> attributes, int contentStart, ReadOnlySpan<byte> tagName)
    {
        if (AuditText.HasAccessibleNameAttribute(attributes))
        {
            return false;
        }

        var closeStart = AuditText.FindCloseTag(html, contentStart, tagName);
        var inner = closeStart < 0 ? html[contentStart..] : html[contentStart..closeStart];
        return !AuditText.HasDiscernibleContent(inner);
    }

    /// <summary>True for a form control that requires a label: <c>&lt;select&gt;</c>, <c>&lt;textarea&gt;</c>, or a non-button-shaped <c>&lt;input&gt;</c>.</summary>
    /// <param name="tagName">Tag name bytes.</param>
    /// <param name="attributes">The element's attribute text.</param>
    /// <returns><see langword="true"/> when the element should carry an accessible name.</returns>
    private static bool IsLabelableControl(ReadOnlySpan<byte> tagName, ReadOnlySpan<byte> attributes)
    {
        if (AsciiByteHelpers.EqualsIgnoreAsciiCase(tagName, "select"u8)
            || AsciiByteHelpers.EqualsIgnoreAsciiCase(tagName, "textarea"u8))
        {
            return true;
        }

        if (!AsciiByteHelpers.EqualsIgnoreAsciiCase(tagName, "input"u8))
        {
            return false;
        }

        if (!HtmlAttr.TryGet(attributes, "type"u8, out var type))
        {
            return true;
        }

        return !IsNonLabelableInputType(AsciiByteHelpers.TrimAsciiWhitespace(type));
    }

    /// <summary>True when <paramref name="type"/> is one of the <c>&lt;input&gt;</c> types that does not need a label.</summary>
    /// <param name="type">Trimmed <c>type</c> attribute value.</param>
    /// <returns><see langword="true"/> for hidden / submit / reset / button / image.</returns>
    private static bool IsNonLabelableInputType(ReadOnlySpan<byte> type)
    {
        for (var i = 0; i < NonLabelableInputTypes.Length; i++)
        {
            if (AsciiByteHelpers.EqualsIgnoreAsciiCase(type, NonLabelableInputTypes[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a form control carries a name attribute or an id referenced by some <c>&lt;label for&gt;</c>.</summary>
    /// <param name="attributes">The control's attribute text.</param>
    /// <param name="labelTargets">Ids referenced by labels on the page.</param>
    /// <returns><see langword="true"/> when the control has an accessible name.</returns>
    private static bool IsControlNamed(ReadOnlySpan<byte> attributes, HashSet<byte[]> labelTargets)
    {
        if (AuditText.HasAccessibleNameAttribute(attributes))
        {
            return true;
        }

        if (!HtmlAttr.TryGet(attributes, "id"u8, out var id) || !AuditText.HasText(id))
        {
            return false;
        }

        return labelTargets.GetAlternateLookup<ReadOnlySpan<byte>>().Contains(AsciiByteHelpers.TrimAsciiWhitespace(id));
    }

    /// <summary>True when the attribute run carries a <c>tabindex</c> whose value parses to a positive integer.</summary>
    /// <param name="attributes">Attribute text from a tag.</param>
    /// <returns><see langword="true"/> for <c>tabindex</c> &gt; 0.</returns>
    private static bool HasPositiveTabIndex(ReadOnlySpan<byte> attributes)
    {
        if (!HtmlAttr.TryGet(attributes, "tabindex"u8, out var value))
        {
            return false;
        }

        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(value);
        return Utf8Parser.TryParse(trimmed, out int parsed, out var consumed) && consumed == trimmed.Length && parsed > 0;
    }
}
