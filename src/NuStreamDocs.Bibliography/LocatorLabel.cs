// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Byte-level case-insensitive classifier that maps a pandoc / AGLC4
/// locator label (e.g. <c>p</c>, <c>pp</c>, <c>para</c>, <c>ch</c>) to a
/// <see cref="LocatorKind"/>. Operates on raw UTF-8 bytes so the scanner
/// never allocates a label string just to switch on it.
/// </summary>
internal static class LocatorLabel
{
    /// <summary>Classifies a label slice; returns <see cref="LocatorKind.Other"/> when nothing matches.</summary>
    /// <param name="label">Raw label bytes (no surrounding whitespace).</param>
    /// <returns>The classified kind.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Single dispatch over the closed pandoc/AGLC4 label vocabulary; flattening into a table just relocates the literals without reducing branching.")]
    public static LocatorKind Classify(ReadOnlySpan<byte> label) => label switch
    {
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "p"u8) => LocatorKind.Page,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "pg"u8) => LocatorKind.Page,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "pp"u8) => LocatorKind.Page,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "page"u8) => LocatorKind.Page,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "pages"u8) => LocatorKind.Page,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "para"u8) => LocatorKind.Paragraph,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "paras"u8) => LocatorKind.Paragraph,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "paragraph"u8) => LocatorKind.Paragraph,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "paragraphs"u8) => LocatorKind.Paragraph,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "l"u8) => LocatorKind.Line,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "line"u8) => LocatorKind.Line,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "lines"u8) => LocatorKind.Line,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "ch"u8) => LocatorKind.Chapter,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "chapter"u8) => LocatorKind.Chapter,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "chapters"u8) => LocatorKind.Chapter,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "s"u8) => LocatorKind.Section,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "ss"u8) => LocatorKind.Section,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "section"u8) => LocatorKind.Section,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "sections"u8) => LocatorKind.Section,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "sch"u8) => LocatorKind.Schedule,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "schedule"u8) => LocatorKind.Schedule,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "schedules"u8) => LocatorKind.Schedule,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "art"u8) => LocatorKind.Article,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "article"u8) => LocatorKind.Article,
        _ when AsciiByteHelpers.EqualsIgnoreAsciiCase(label, "articles"u8) => LocatorKind.Article,
        _ => LocatorKind.Other
    };
}
