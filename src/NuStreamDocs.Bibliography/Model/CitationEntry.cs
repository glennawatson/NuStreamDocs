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
/// <para>
/// All free-text fields are <see cref="byte"/> arrays in UTF-8 so the AGLC4 emitter copies them
/// straight into its <see cref="System.Buffers.IBufferWriter{T}"/> sink without per-emit
/// transcoding. The CSL JSON loader and per-page citation rewriter both produce byte arrays
/// directly; <see cref="BibliographyDatabaseBuilder"/> exposes string-shaped <c>Add…</c> helpers
/// at the public construction boundary that encode once.
/// </para>
/// </remarks>
public sealed record CitationEntry
{
    /// <summary>Gets the citation key bytes — the <c>id</c> a markdown <c>[@key]</c> resolves against.</summary>
    public required byte[] Id { get; init; }

    /// <summary>Gets the entry type (drives style selection per CSL).</summary>
    public required EntryType Type { get; init; }

    /// <summary>Gets the primary title bytes (book title, article title, case name, statute short name).</summary>
    public byte[] Title { get; init; } = [];

    /// <summary>Gets a short version of the title used for repeat citations and bibliography back-references (CSL <c>title-short</c>).</summary>
    public byte[] ShortTitle { get; init; } = [];

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

    /// <summary>Gets the journal / book / proceedings name bytes (CSL <c>container-title</c>).</summary>
    public byte[] ContainerTitle { get; init; } = [];

    /// <summary>Gets the publisher bytes.</summary>
    public byte[] Publisher { get; init; } = [];

    /// <summary>Gets the place-of-publication bytes.</summary>
    public byte[] PublisherPlace { get; init; } = [];

    /// <summary>Gets the journal-volume bytes.</summary>
    public byte[] Volume { get; init; } = [];

    /// <summary>Gets the journal-issue bytes.</summary>
    public byte[] Issue { get; init; } = [];

    /// <summary>Gets the page-range bytes (e.g. <c>"123-145"</c>).</summary>
    public byte[] Page { get; init; } = [];

    /// <summary>Gets the canonical URL bytes (CSL <c>URL</c>).</summary>
    public byte[] Url { get; init; } = [];

    /// <summary>Gets the DOI bytes (CSL <c>DOI</c>); rendered with <c>doi.org</c> prefix.</summary>
    public byte[] Doi { get; init; } = [];

    /// <summary>Gets free-form note bytes (CSL <c>note</c>).</summary>
    public byte[] Note { get; init; } = [];

    /// <summary>Gets the court / tribunal-name bytes for legal cases (e.g. <c>"HCA"</c>, <c>"NSWCA"</c>).</summary>
    public byte[] Court { get; init; } = [];

    /// <summary>Gets the jurisdiction-code bytes for legislation (e.g. <c>"Cth"</c>, <c>"NSW"</c>).</summary>
    public byte[] Jurisdiction { get; init; } = [];

    /// <summary>Gets the AGLC4 law-report-series citation bytes (e.g. <c>"(1992) 175 CLR 1"</c>) when the case is reported.</summary>
    public byte[] LawReportSeries { get; init; } = [];

    /// <summary>Gets the medium-neutral citation bytes for unreported / online cases (e.g. <c>"[2020] HCA 5"</c>).</summary>
    public byte[] MediumNeutralCitation { get; init; } = [];
}
