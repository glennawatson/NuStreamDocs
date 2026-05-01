// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Csl;

/// <summary>
/// Loads a CSL-JSON bibliography file (the canonical machine-readable
/// format used by pandoc, citation.js, Zotero export). Field names
/// match CSL's vocabulary exactly so this is the no-translation-needed
/// path for users who already maintain their bibliography in CSL-JSON.
/// </summary>
/// <remarks>
/// Hot-path notes — property-name lookups all use the UTF-8 overload
/// of <see cref="JsonElement.TryGetProperty(ReadOnlySpan{byte}, out JsonElement)"/>
/// with <c>u8</c> string literals so the comparison stays byte-for-byte
/// with no per-call string interning. <see cref="JsonDocument.Parse(ReadOnlyMemory{byte}, JsonDocumentOptions)"/>
/// takes the buffer without copying.
/// </remarks>
internal static class CslJsonLoader
{
    /// <summary>Loads a CSL-JSON file from disk and returns the parsed entries.</summary>
    /// <param name="path">Path to a <c>.json</c> file containing a CSL-JSON array.</param>
    /// <returns>Parsed entries.</returns>
    public static IReadOnlyList<CitationEntry> LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = File.ReadAllBytes(path);
        return Parse(bytes);
    }

    /// <summary>Parses a CSL-JSON UTF-8 buffer into entries.</summary>
    /// <param name="json">UTF-8 source.</param>
    /// <returns>Parsed entries.</returns>
    public static IReadOnlyList<CitationEntry> Parse(ReadOnlyMemory<byte> json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var entries = new List<CitationEntry>(doc.RootElement.GetArrayLength());
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (TryParseEntry(element, out var entry))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>Parses one CSL-JSON object into a <see cref="CitationEntry"/>.</summary>
    /// <param name="element">JSON object element.</param>
    /// <param name="entry">Parsed entry on success.</param>
    /// <returns>True when the element parsed cleanly (id and type present).</returns>
    private static bool TryParseEntry(JsonElement element, out CitationEntry entry)
    {
        entry = null!;
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("id"u8, out var idElement) || idElement.GetString() is not { Length: > 0 } id)
        {
            return false;
        }

        entry = BuildPrintFields(element, id) with
        {
            Court = GetString(element, "authority"u8),
            Jurisdiction = GetString(element, "jurisdiction"u8),
            LawReportSeries = GetString(element, "references"u8),
            MediumNeutralCitation = GetString(element, "number"u8),
        };
        return true;
    }

    /// <summary>Builds the print-side fields of a CSL-JSON entry.</summary>
    /// <param name="element">JSON object element.</param>
    /// <param name="id">Already-validated <c>id</c>.</param>
    /// <returns>Entry populated with the non-legal fields.</returns>
    private static CitationEntry BuildPrintFields(JsonElement element, string id) => new()
    {
        Id = id,
        Type = ParseType(GetString(element, "type"u8)),
        Title = GetString(element, "title"u8),
        ShortTitle = GetString(element, "title-short"u8),
        Authors = ParseNames(element, "author"u8),
        Editors = ParseNames(element, "editor"u8),
        Year = ParseYear(element),
        ContainerTitle = GetString(element, "container-title"u8),
        Publisher = GetString(element, "publisher"u8),
        PublisherPlace = GetString(element, "publisher-place"u8),
        Volume = GetString(element, "volume"u8),
        Issue = GetString(element, "issue"u8),
        Page = GetString(element, "page"u8),
        Url = GetString(element, "URL"u8),
        Doi = GetString(element, "DOI"u8),
        Note = GetString(element, "note"u8),
    };

    /// <summary>Maps a CSL <c>type</c> string to our <see cref="EntryType"/> enum.</summary>
    /// <param name="type">CSL type string (kebab-case).</param>
    /// <returns>The mapped <see cref="EntryType"/>; <see cref="EntryType.Other"/> when unknown.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Single switch expression mapping CSL type strings to enum values.")]
    private static EntryType ParseType(string type) => type switch
    {
        "book" => EntryType.Book,
        "chapter" => EntryType.Chapter,
        "article-journal" => EntryType.ArticleJournal,
        "article-magazine" => EntryType.ArticleMagazine,
        "article-newspaper" => EntryType.ArticleNewspaper,
        "article" => EntryType.Article,
        "legal_case" => EntryType.LegalCase,
        "legislation" => EntryType.Legislation,
        "treaty" => EntryType.Treaty,
        "report" => EntryType.Report,
        "paper-conference" => EntryType.PaperConference,
        "thesis" => EntryType.Thesis,
        "webpage" => EntryType.Webpage,
        "manuscript" => EntryType.Manuscript,
        _ => EntryType.Other,
    };

    /// <summary>Reads <c>issued.date-parts[0][0]</c> per CSL-JSON.</summary>
    /// <param name="element">Entry object.</param>
    /// <returns>The parsed year, or 0 when absent.</returns>
    private static int ParseYear(JsonElement element)
    {
        if (!element.TryGetProperty("issued"u8, out var issued)
            || !issued.TryGetProperty("date-parts"u8, out var parts)
            || parts.ValueKind is not JsonValueKind.Array
            || parts.GetArrayLength() is 0)
        {
            return 0;
        }

        var first = parts[0];
        if (first.ValueKind is not JsonValueKind.Array || first.GetArrayLength() is 0)
        {
            return 0;
        }

        return first[0].TryGetInt32(out var year) ? year : 0;
    }

    /// <summary>Parses a CSL name array into our <see cref="PersonName"/>[] shape.</summary>
    /// <param name="element">Entry object.</param>
    /// <param name="utf8Property">Property name as UTF-8 (<c>"author"u8</c> / <c>"editor"u8</c>).</param>
    /// <returns>Parsed names; empty array when absent.</returns>
    private static PersonName[] ParseNames(JsonElement element, ReadOnlySpan<byte> utf8Property)
    {
        if (!element.TryGetProperty(utf8Property, out var array) || array.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var result = new PersonName[array.GetArrayLength()];
        var i = 0;
        foreach (var item in array.EnumerateArray())
        {
            result[i++] = new(
                Family: GetString(item, "family"u8),
                Given: GetString(item, "given"u8),
                Suffix: GetString(item, "suffix"u8),
                Literal: GetString(item, "literal"u8));
        }

        return result;
    }

    /// <summary>Reads a string property; returns empty string when absent or wrong type.</summary>
    /// <param name="element">JSON object.</param>
    /// <param name="utf8Property">Property name as UTF-8 — supplied via the <c>u8</c> literal.</param>
    /// <returns>String value or empty.</returns>
    private static string GetString(JsonElement element, ReadOnlySpan<byte> utf8Property) =>
        element.TryGetProperty(utf8Property, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
