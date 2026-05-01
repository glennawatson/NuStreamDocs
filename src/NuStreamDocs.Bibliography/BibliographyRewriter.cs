// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Walks markdown source, replaces every recognised <c>[@key]</c> with
/// the style's in-text marker (a numbered footnote reference for
/// AGLC4), and appends a Bibliography section listing every cited
/// source in citation order. All output is byte-by-byte — no
/// intermediate <see cref="string"/> per emit.
/// </summary>
internal static class BibliographyRewriter
{
    /// <summary>Gets the UTF-8 separator between the original source and the appended footnote / bibliography blocks.</summary>
    private static ReadOnlySpan<byte> SectionBreak => "\n\n"u8;

    /// <summary>Gets the UTF-8 footnote-definition prefix.</summary>
    private static ReadOnlySpan<byte> FootnotePrefix => "[^bib-"u8;

    /// <summary>Gets the bibliography section heading.</summary>
    private static ReadOnlySpan<byte> BibliographyHeading => "## Bibliography\n\n"u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>; emits footnote definitions and a bibliography section after the body.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <param name="database">Resolved citation database.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="missing">Optional callback fired for unresolved keys.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when at least one citation was rewritten.</returns>
    public static bool Rewrite(ReadOnlySpan<byte> source, BibliographyDatabase database, ICitationStyle style, MissingCitationCallback? missing, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(writer);

        var markers = CitationMarkerScanner.Find(source);
        if (markers.Count is 0)
        {
            return false;
        }

        var assignments = AssignFootnoteNumbers(markers, database, missing);
        if (assignments.UsedNumbers is 0)
        {
            return false;
        }

        EmitBodyWithMarkers(source, markers, assignments, style, writer);
        EmitFootnoteDefinitions(assignments, style, writer);
        EmitBibliography(assignments, style, writer);
        return true;
    }

    /// <summary>Resolves keys + assigns sequential footnote numbers.</summary>
    /// <param name="markers">Parsed markers in source order.</param>
    /// <param name="database">Citation database.</param>
    /// <param name="missing">Missing-callback.</param>
    /// <returns>Footnote-assignment bundle.</returns>
    private static Assignments AssignFootnoteNumbers(IReadOnlyList<CitationMarker> markers, BibliographyDatabase database, MissingCitationCallback? missing)
    {
        var markerNumbers = new int[markers.Count][];
        var locatorByNum = new List<CitationLocator>(markers.Count) { CitationLocator.None };
        var entryByNum = new List<CitationEntry>(markers.Count) { null! };
        var unique = new List<CitationEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            var numbers = new int[marker.Cites.Length];
            for (var j = 0; j < marker.Cites.Length; j++)
            {
                var cite = marker.Cites[j];
                if (!database.TryGet(cite.Key, out var entry) || entry is null)
                {
                    missing?.Invoke(cite.Key);
                    numbers[j] = 0;
                    continue;
                }

                numbers[j] = locatorByNum.Count;
                locatorByNum.Add(cite.Locator);
                entryByNum.Add(entry);
                if (seen.Add(entry.Id))
                {
                    unique.Add(entry);
                }
            }

            markerNumbers[i] = numbers;
        }

        return new Assignments(
            UsedNumbers: locatorByNum.Count - 1,
            MarkerNumbers: markerNumbers,
            UniqueOrder: unique,
            LocatorByNumber: [.. locatorByNum],
            EntryByNumber: [.. entryByNum]);
    }

    /// <summary>Writes the body bytes through, replacing each marker with the style's in-text reference(s).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="markers">Markers in order.</param>
    /// <param name="assignments">Footnote assignments.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitBodyWithMarkers(ReadOnlySpan<byte> source, IReadOnlyList<CitationMarker> markers, Assignments assignments, ICitationStyle style, IBufferWriter<byte> writer)
    {
        var cursor = 0;
        for (var i = 0; i < markers.Count; i++)
        {
            var marker = markers[i];
            Write(writer, source[cursor..marker.StartIndex]);
            EmitInTextRefs(assignments, i, style, writer);
            cursor = marker.EndIndex;
        }

        Write(writer, source[cursor..]);
    }

    /// <summary>Emits the in-text refs for one marker (semicolon-joined when multi-cite).</summary>
    /// <param name="assignments">Assignments.</param>
    /// <param name="markerIndex">Marker position.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitInTextRefs(Assignments assignments, int markerIndex, ICitationStyle style, IBufferWriter<byte> writer)
    {
        var numbers = assignments.MarkerNumbers[markerIndex];
        var first = true;
        for (var j = 0; j < numbers.Length; j++)
        {
            var num = numbers[j];
            if (num is 0)
            {
                continue;
            }

            if (!first)
            {
                Write(writer, "; "u8);
            }

            var entry = assignments.EntryByNumber[num];
            style.WriteInText(entry, num, writer);
            first = false;
        }
    }

    /// <summary>Appends one footnote definition per assigned number — <c>[^bib-id]: rendered citation</c>.</summary>
    /// <param name="assignments">Assignments.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitFootnoteDefinitions(Assignments assignments, ICitationStyle style, IBufferWriter<byte> writer)
    {
        if (assignments.UsedNumbers is 0)
        {
            return;
        }

        Write(writer, SectionBreak);
        for (var n = 1; n <= assignments.UsedNumbers; n++)
        {
            var entry = assignments.EntryByNumber[n];
            var locator = assignments.LocatorByNumber[n];
            Write(writer, FootnotePrefix);
            WriteUtf8(entry.Id, writer);
            Write(writer, "]: "u8);
            style.WriteFootnote(entry, locator, writer);
            Write(writer, "\n"u8);
        }
    }

    /// <summary>Appends the Bibliography section listing every unique cited source in citation order.</summary>
    /// <param name="assignments">Assignments.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitBibliography(Assignments assignments, ICitationStyle style, IBufferWriter<byte> writer)
    {
        if (assignments.UniqueOrder.Count is 0)
        {
            return;
        }

        Write(writer, SectionBreak);
        Write(writer, BibliographyHeading);
        for (var i = 0; i < assignments.UniqueOrder.Count; i++)
        {
            WriteInt(i + 1, writer);
            Write(writer, ". "u8);
            style.WriteBibliography(assignments.UniqueOrder[i], writer);
            Write(writer, "\n"u8);
        }
    }

    /// <summary>Encodes <paramref name="value"/> directly to UTF-8 in the sink.</summary>
    /// <param name="value">Source string.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteUtf8(string value, IBufferWriter<byte> writer)
    {
        if (value.Length is 0)
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var dst = writer.GetSpan(max);
        var written = Encoding.UTF8.GetBytes(value, dst);
        writer.Advance(written);
    }

    /// <summary>Writes an integer as ASCII via <see cref="Utf8Formatter"/>.</summary>
    /// <param name="value">Integer value.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteInt(int value, IBufferWriter<byte> writer)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            return;
        }

        Write(writer, buffer[..written]);
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Per-cite footnote assignment + locator carry-through.</summary>
    /// <param name="UsedNumbers">Total footnotes assigned across the page.</param>
    /// <param name="MarkerNumbers">For each marker (indexed parallel to <c>markers</c>), the assigned footnote numbers per inner reference.</param>
    /// <param name="UniqueOrder">Each cited entry once, in first-citation order.</param>
    /// <param name="LocatorByNumber">Locator per assigned footnote number (1-based; index 0 unused).</param>
    /// <param name="EntryByNumber">Resolved entry per assigned footnote number (1-based).</param>
    private readonly record struct Assignments(
        int UsedNumbers,
        int[][] MarkerNumbers,
        List<CitationEntry> UniqueOrder,
        CitationLocator[] LocatorByNumber,
        CitationEntry[] EntryByNumber);
}
