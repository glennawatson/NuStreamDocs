// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography.Model;

/// <summary>
/// Classified locator label — the pandoc / AGLC4 universe of pinpoint
/// kinds. <see cref="None"/> means "bare value, no label was present";
/// the remaining members map to a concrete formatter prefix or shape.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1028:Enum storage should be Int32",
    Justification = "Closed vocabulary of <16 members; byte storage halves CitationLocator footprint.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Sonar Code Smell",
    "S4022:Enums storage should be Int32",
    Justification = "Closed vocabulary of <16 members; byte storage halves CitationLocator footprint.")]
public enum LocatorKind : byte
{
    /// <summary>No label present (<c>[@key, 23]</c>).</summary>
    None = 0,

    /// <summary>Page-style label (<c>p</c>, <c>pp</c>, <c>page</c>, …) — emit as bare value.</summary>
    Page = 1,

    /// <summary>Paragraph (<c>para</c>, <c>paragraph</c>) — emit as <c>[value]</c>.</summary>
    Paragraph = 2,

    /// <summary>Line (<c>l</c>, <c>line</c>).</summary>
    Line = 3,

    /// <summary>Chapter (<c>ch</c>, <c>chapter</c>).</summary>
    Chapter = 4,

    /// <summary>Section (<c>s</c>, <c>section</c>, <c>ss</c>).</summary>
    Section = 5,

    /// <summary>Schedule (<c>sch</c>, <c>schedule</c>).</summary>
    Schedule = 6,

    /// <summary>Article (<c>art</c>, <c>article</c>).</summary>
    Article = 7,

    /// <summary>Unknown label — emitted verbatim with the value (rare; mainly for forward-compat).</summary>
    Other = 255,
}
