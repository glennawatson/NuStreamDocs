// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>Maps a pandoc / AGLC4 locator label (e.g. <c>p</c>, <c>pp</c>, <c>para</c>, <c>ch</c>) to a <see cref="LocatorKind"/>.</summary>
internal static class LocatorLabel
{
    /// <summary>Length of the longest recognized label (<c>paragraphs</c>); longer inputs never match.</summary>
    private const int MaxLabelLength = 10;

    /// <summary>Recognized labels (lowercase ASCII) mapped to their kind.</summary>
    private static readonly Dictionary<byte[], LocatorKind> KindsByLabel = CreateKindsByLabel();

    /// <summary>Classifies a label slice; returns <see cref="LocatorKind.Other"/> when nothing matches.</summary>
    /// <param name="label">Raw label bytes (no surrounding whitespace).</param>
    /// <returns>The classified kind.</returns>
    internal static LocatorKind Classify(ReadOnlySpan<byte> label)
    {
        if (label.Length is 0 or > MaxLabelLength)
        {
            return LocatorKind.Other;
        }

        Span<byte> lowered = stackalloc byte[MaxLabelLength];
        for (var i = 0; i < label.Length; i++)
        {
            lowered[i] = AsciiByteHelpers.ToAsciiLowerByte(label[i]);
        }

        return KindsByLabel.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(lowered[..label.Length], out var kind)
            ? kind
            : LocatorKind.Other;
    }

    /// <summary>Creates the recognized locator labels.</summary>
    /// <returns>The name-to-kind lookup.</returns>
    private static Dictionary<byte[], LocatorKind> CreateKindsByLabel()
    {
        Dictionary<byte[], LocatorKind> values = [with(ByteArrayComparer.Instance)];
        values[[.. "p"u8]] = LocatorKind.Page;
        values[[.. "pg"u8]] = LocatorKind.Page;
        values[[.. "pp"u8]] = LocatorKind.Page;
        values[[.. "page"u8]] = LocatorKind.Page;
        values[[.. "pages"u8]] = LocatorKind.Page;
        values[[.. "para"u8]] = LocatorKind.Paragraph;
        values[[.. "paras"u8]] = LocatorKind.Paragraph;
        values[[.. "paragraph"u8]] = LocatorKind.Paragraph;
        values[[.. "paragraphs"u8]] = LocatorKind.Paragraph;
        values[[.. "l"u8]] = LocatorKind.Line;
        values[[.. "line"u8]] = LocatorKind.Line;
        values[[.. "lines"u8]] = LocatorKind.Line;
        values[[.. "ch"u8]] = LocatorKind.Chapter;
        values[[.. "chapter"u8]] = LocatorKind.Chapter;
        values[[.. "chapters"u8]] = LocatorKind.Chapter;
        values[[.. "s"u8]] = LocatorKind.Section;
        values[[.. "ss"u8]] = LocatorKind.Section;
        values[[.. "section"u8]] = LocatorKind.Section;
        values[[.. "sections"u8]] = LocatorKind.Section;
        values[[.. "sch"u8]] = LocatorKind.Schedule;
        values[[.. "schedule"u8]] = LocatorKind.Schedule;
        values[[.. "schedules"u8]] = LocatorKind.Schedule;
        values[[.. "art"u8]] = LocatorKind.Article;
        values[[.. "article"u8]] = LocatorKind.Article;
        values[[.. "articles"u8]] = LocatorKind.Article;
        return values;
    }
}
