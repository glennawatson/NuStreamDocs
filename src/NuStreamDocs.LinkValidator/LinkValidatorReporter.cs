// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Stateless helpers extracted from <see cref="LinkValidatorPlugin"/>:
/// the bits that don't touch HTTP / the file system, so they can be
/// unit-tested directly without standing up a corpus.
/// </summary>
internal static class LinkValidatorReporter
{
    /// <summary>Sums up internal/external link counts across every page in <paramref name="corpus"/>.</summary>
    /// <param name="corpus">Pre-built corpus.</param>
    /// <returns>Total internal + external link counts.</returns>
    public static (int Internal, int External) CountLinks(ValidationCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        var internalLinkCount = 0;
        var externalLinkCount = 0;
        for (var p = 0; p < corpus.Pages.Length; p++)
        {
            internalLinkCount += corpus.Pages[p].InternalLinks.Length;
            externalLinkCount += corpus.Pages[p].ExternalLinks.Length;
        }

        return (internalLinkCount, externalLinkCount);
    }

    /// <summary>Merges the two validator outputs and demotes severities when the matching strict flag is off.</summary>
    /// <param name="internalDiags">Diagnostics from the internal validator.</param>
    /// <param name="externalDiags">Diagnostics from the external validator.</param>
    /// <param name="strictInternal">When true, internal diagnostics keep their reported severity; otherwise they're demoted to <see cref="LinkSeverity.Warning"/>.</param>
    /// <param name="strictExternal">Same as <paramref name="strictInternal"/> for external diagnostics.</param>
    /// <returns>One merged, severity-adjusted array.</returns>
    public static LinkDiagnostic[] Merge(
        LinkDiagnostic[] internalDiags,
        LinkDiagnostic[] externalDiags,
        bool strictInternal,
        bool strictExternal)
    {
        ArgumentNullException.ThrowIfNull(internalDiags);
        ArgumentNullException.ThrowIfNull(externalDiags);

        var merged = new LinkDiagnostic[internalDiags.Length + externalDiags.Length];
        for (var i = 0; i < internalDiags.Length; i++)
        {
            merged[i] = AdjustSeverity(internalDiags[i], strictInternal);
        }

        for (var i = 0; i < externalDiags.Length; i++)
        {
            merged[internalDiags.Length + i] = AdjustSeverity(externalDiags[i], strictExternal);
        }

        return merged;
    }

    /// <summary>Splits a diagnostic stream into broken (error) and warning counts.</summary>
    /// <param name="diagnostics">Combined diagnostics.</param>
    /// <returns>Broken-count and warning-count.</returns>
    public static (int Broken, int Warnings) Tally(LinkDiagnostic[] diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var broken = 0;
        var warnings = 0;
        for (var i = 0; i < diagnostics.Length; i++)
        {
            if (diagnostics[i].Severity == LinkSeverity.Error)
            {
                broken++;
            }
            else
            {
                warnings++;
            }
        }

        return (broken, warnings);
    }

    /// <summary>Returns true when at least one diagnostic is severity-error.</summary>
    /// <param name="diagnostics">Diagnostics to scan.</param>
    /// <returns>True when fatal.</returns>
    public static bool HasFatal(LinkDiagnostic[] diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        for (var i = 0; i < diagnostics.Length; i++)
        {
            if (diagnostics[i].Severity == LinkSeverity.Error)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Demotes a diagnostic to warning when the matching strict flag is off.</summary>
    /// <param name="diag">Original diagnostic.</param>
    /// <param name="strict">Whether the relevant strict flag is on.</param>
    /// <returns>The diagnostic, possibly with severity downgraded.</returns>
    public static LinkDiagnostic AdjustSeverity(in LinkDiagnostic diag, bool strict) =>
        strict ? diag : diag with { Severity = LinkSeverity.Warning };
}
