// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>Performance lint flagging render-blocking <c>&lt;script src&gt;</c> elements inside <c>&lt;head&gt;</c>.</summary>
internal static class ScriptAudit
{
    /// <summary>Diagnostic message for a render-blocking head script.</summary>
    private const string RenderBlockingMessage =
        "<script src> in <head> is render-blocking. Add async, defer, or type=\"module\", or move it before </body>.";

    /// <summary>Runs the script lint over one page.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="page">Site-relative URL of the page.</param>
    /// <param name="options">Audit options (rule toggles).</param>
    /// <param name="sink">Receives the findings.</param>
    public static void Check(ReadOnlySpan<byte> html, UrlPath page, AuditOptions options, List<AuditDiagnostic> sink)
    {
        if (!options.IsRuleEnabled(AuditRule.RenderBlockingScript))
        {
            return;
        }

        var inHead = false;
        HtmlTagCursor cursor = new(html);
        while (cursor.MoveNext())
        {
            if (AsciiByteHelpers.EqualsIgnoreAsciiCase(cursor.Name, "head"u8))
            {
                inHead = !cursor.IsEndTag;
                continue;
            }

            if (!inHead || cursor.IsEndTag || !AsciiByteHelpers.EqualsIgnoreAsciiCase(cursor.Name, "script"u8))
            {
                continue;
            }

            if (cursor.HasAttribute("src"u8) && !IsDeferredLoad(cursor.Attributes))
            {
                sink.Add(new(page, AuditRule.RenderBlockingScript, RenderBlockingMessage));
            }
        }
    }

    /// <summary>True when the script declares <c>async</c>, <c>defer</c>, or <c>type="module"</c> (which is deferred by default).</summary>
    /// <param name="attributes">Attribute text from the <c>&lt;script&gt;</c> tag.</param>
    /// <returns><see langword="true"/> when the script does not block first paint.</returns>
    private static bool IsDeferredLoad(ReadOnlySpan<byte> attributes)
    {
        if (HtmlAttr.Has(attributes, "async"u8) || HtmlAttr.Has(attributes, "defer"u8))
        {
            return true;
        }

        return HtmlAttr.TryGet(attributes, "type"u8, out var type)
            && AsciiByteHelpers.EqualsIgnoreAsciiCase(AsciiByteHelpers.TrimAsciiWhitespace(type), "module"u8);
    }
}
