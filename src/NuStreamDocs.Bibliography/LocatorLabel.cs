// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Bibliography.Model;

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
        _ when EqualsIgnoreCase(label, "p"u8) => LocatorKind.Page,
        _ when EqualsIgnoreCase(label, "pg"u8) => LocatorKind.Page,
        _ when EqualsIgnoreCase(label, "pp"u8) => LocatorKind.Page,
        _ when EqualsIgnoreCase(label, "page"u8) => LocatorKind.Page,
        _ when EqualsIgnoreCase(label, "pages"u8) => LocatorKind.Page,
        _ when EqualsIgnoreCase(label, "para"u8) => LocatorKind.Paragraph,
        _ when EqualsIgnoreCase(label, "paras"u8) => LocatorKind.Paragraph,
        _ when EqualsIgnoreCase(label, "paragraph"u8) => LocatorKind.Paragraph,
        _ when EqualsIgnoreCase(label, "paragraphs"u8) => LocatorKind.Paragraph,
        _ when EqualsIgnoreCase(label, "l"u8) => LocatorKind.Line,
        _ when EqualsIgnoreCase(label, "line"u8) => LocatorKind.Line,
        _ when EqualsIgnoreCase(label, "lines"u8) => LocatorKind.Line,
        _ when EqualsIgnoreCase(label, "ch"u8) => LocatorKind.Chapter,
        _ when EqualsIgnoreCase(label, "chapter"u8) => LocatorKind.Chapter,
        _ when EqualsIgnoreCase(label, "chapters"u8) => LocatorKind.Chapter,
        _ when EqualsIgnoreCase(label, "s"u8) => LocatorKind.Section,
        _ when EqualsIgnoreCase(label, "ss"u8) => LocatorKind.Section,
        _ when EqualsIgnoreCase(label, "section"u8) => LocatorKind.Section,
        _ when EqualsIgnoreCase(label, "sections"u8) => LocatorKind.Section,
        _ when EqualsIgnoreCase(label, "sch"u8) => LocatorKind.Schedule,
        _ when EqualsIgnoreCase(label, "schedule"u8) => LocatorKind.Schedule,
        _ when EqualsIgnoreCase(label, "schedules"u8) => LocatorKind.Schedule,
        _ when EqualsIgnoreCase(label, "art"u8) => LocatorKind.Article,
        _ when EqualsIgnoreCase(label, "article"u8) => LocatorKind.Article,
        _ when EqualsIgnoreCase(label, "articles"u8) => LocatorKind.Article,
        _ => LocatorKind.Other,
    };

    /// <summary>ASCII case-insensitive equality on UTF-8 bytes.</summary>
    /// <param name="left">Candidate.</param>
    /// <param name="right">Lowercase reference (must contain only ASCII letters, digits, and ASCII punctuation).</param>
    /// <returns>True when equal under ASCII case-fold.</returns>
    private static bool EqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (ToLowerAscii(left[i]) != right[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lower-cases an ASCII byte (no-op for non-letters).</summary>
    /// <param name="b">Source byte.</param>
    /// <returns>Lowercased byte.</returns>
    private static byte ToLowerAscii(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + ('a' - 'A')) : b;
}
