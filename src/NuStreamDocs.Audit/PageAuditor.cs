// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>Runs every enabled accessibility and performance lint over a single rendered page.</summary>
public static class PageAuditor
{
    /// <summary>Audits one page's HTML.</summary>
    /// <param name="page">Site-relative URL of the page (used only to label the findings).</param>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="options">Audit options (rule toggles).</param>
    /// <returns>The findings, in lint order; empty when the page is a meta-refresh redirect stub or nothing fired.</returns>
    public static AuditDiagnostic[] Audit(UrlPath page, ReadOnlySpan<byte> html, AuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Meta-refresh redirect stubs are bouncers, not real content — auditing them is noise.
        if (html.IndexOf("http-equiv=\"refresh\""u8) >= 0)
        {
            return [];
        }

        List<AuditDiagnostic> findings = [];
        ImageAudit.Check(html, page, options, findings);
        DocumentStructureAudit.Check(html, page, options, findings);
        InteractiveAudit.Check(html, page, options, findings);
        ScriptAudit.Check(html, page, options, findings);
        return [.. findings];
    }
}
