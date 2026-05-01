// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography.Model;

/// <summary>
/// Single bibliography entry. Field names + types align with CSL-JSON's
/// item model so a future CSL backend reads the same data unchanged.
/// </summary>
/// <remarks>
/// AGLC4 doesn't use every CSL field; unused values are kept empty.
/// AGLC4-specific extras (<see cref="ShortTitle"/>, <see cref="Court"/>,
/// <see cref="Jurisdiction"/>, <see cref="LawReportSeries"/>) live
/// alongside the CSL fields without breaking the CSL shape.
/// </remarks>
public sealed record CitationEntry
{
    /// <summary>Gets the citation key — the <c>id</c> a markdown <c>[@key]</c> resolves against.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the entry type (drives style selection per CSL).</summary>
    public required EntryType Type { get; init; }

    /// <summary>Gets the primary title (book title, article title, case name, statute short name).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets a short version of the title used for repeat citations and bibliography back-references (CSL <c>title-short</c>).</summary>
    public string ShortTitle { get; init; } = string.Empty;

    /// <summary>Gets the authors / contributors (CSL <c>author</c>); empty for institutional or anonymous works.</summary>
    public PersonName[] Authors { get; init; } = [];

    /// <summary>Gets the editors (CSL <c>editor</c>).</summary>
    public PersonName[] Editors { get; init; } = [];

    /// <summary>Gets the publication year (CSL <c>issued.date-parts[0][0]</c>); 0 when unknown.</summary>
    public int Year { get; init; }

    /// <summary>Gets the publication month (1-12); 0 when unknown.</summary>
    public int Month { get; init; }

    /// <summary>Gets the publication day (1-31); 0 when unknown.</summary>
    public int Day { get; init; }

    /// <summary>Gets the journal / book / proceedings name (CSL <c>container-title</c>).</summary>
    public string ContainerTitle { get; init; } = string.Empty;

    /// <summary>Gets the publisher.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Gets the place of publication.</summary>
    public string PublisherPlace { get; init; } = string.Empty;

    /// <summary>Gets the journal volume.</summary>
    public string Volume { get; init; } = string.Empty;

    /// <summary>Gets the journal issue.</summary>
    public string Issue { get; init; } = string.Empty;

    /// <summary>Gets the page range (e.g. <c>"123-145"</c>).</summary>
    public string Page { get; init; } = string.Empty;

    /// <summary>Gets the canonical URL (CSL <c>URL</c>).</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Gets the DOI (CSL <c>DOI</c>); rendered with <c>doi.org</c> prefix.</summary>
    public string Doi { get; init; } = string.Empty;

    /// <summary>Gets a free-form note (CSL <c>note</c>).</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>Gets the court / tribunal name for legal cases (e.g. <c>"HCA"</c>, <c>"NSWCA"</c>).</summary>
    public string Court { get; init; } = string.Empty;

    /// <summary>Gets the jurisdiction code for legislation (e.g. <c>"Cth"</c>, <c>"NSW"</c>).</summary>
    public string Jurisdiction { get; init; } = string.Empty;

    /// <summary>Gets the AGLC4 law-report series citation (e.g. <c>"(1992) 175 CLR 1"</c>) when the case is reported.</summary>
    public string LawReportSeries { get; init; } = string.Empty;

    /// <summary>Gets the medium-neutral citation for unreported / online cases (e.g. <c>"[2020] HCA 5"</c>).</summary>
    public string MediumNeutralCitation { get; init; } = string.Empty;
}
