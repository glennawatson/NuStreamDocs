// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Maps a pandoc / AGLC4 locator label (e.g. <c>p</c>, <c>pp</c>, <c>para</c>,
/// <c>ch</c>) to a <see cref="LocatorKind"/>.
/// </summary>
internal static class LocatorLabel
{
    /// <summary>Length of the longest recognized label (<c>paragraphs</c>); longer inputs never match.</summary>
    private const int MaxLabelLength = 10;

    /// <summary>Recognized labels (lowercase ASCII) mapped to their kind.</summary>
    private static readonly Dictionary<byte[], LocatorKind> KindsByLabel = new(ByteArrayComparer.Instance)
    {
        [[.. "p"u8]] = LocatorKind.Page,
        [[.. "pg"u8]] = LocatorKind.Page,
        [[.. "pp"u8]] = LocatorKind.Page,
        [[.. "page"u8]] = LocatorKind.Page,
        [[.. "pages"u8]] = LocatorKind.Page,
        [[.. "para"u8]] = LocatorKind.Paragraph,
        [[.. "paras"u8]] = LocatorKind.Paragraph,
        [[.. "paragraph"u8]] = LocatorKind.Paragraph,
        [[.. "paragraphs"u8]] = LocatorKind.Paragraph,
        [[.. "l"u8]] = LocatorKind.Line,
        [[.. "line"u8]] = LocatorKind.Line,
        [[.. "lines"u8]] = LocatorKind.Line,
        [[.. "ch"u8]] = LocatorKind.Chapter,
        [[.. "chapter"u8]] = LocatorKind.Chapter,
        [[.. "chapters"u8]] = LocatorKind.Chapter,
        [[.. "s"u8]] = LocatorKind.Section,
        [[.. "ss"u8]] = LocatorKind.Section,
        [[.. "section"u8]] = LocatorKind.Section,
        [[.. "sections"u8]] = LocatorKind.Section,
        [[.. "sch"u8]] = LocatorKind.Schedule,
        [[.. "schedule"u8]] = LocatorKind.Schedule,
        [[.. "schedules"u8]] = LocatorKind.Schedule,
        [[.. "art"u8]] = LocatorKind.Article,
        [[.. "article"u8]] = LocatorKind.Article,
        [[.. "articles"u8]] = LocatorKind.Article
    };

    /// <summary>Classifies a label slice; returns <see cref="LocatorKind.Other"/> when nothing matches.</summary>
    /// <param name="label">Raw label bytes (no surrounding whitespace).</param>
    /// <returns>The classified kind.</returns>
    public static LocatorKind Classify(ReadOnlySpan<byte> label)
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
}
