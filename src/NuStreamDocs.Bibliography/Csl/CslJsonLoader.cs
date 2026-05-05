// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography.Csl;

/// <summary>
/// Loads a CSL-JSON bibliography file (the canonical machine-readable
/// format used by pandoc, citation.js, Zotero export). Field names
/// match CSL's vocabulary exactly so this is the no-translation-needed
/// path for users who already maintain their bibliography in CSL-JSON.
/// </summary>
/// <remarks>
/// Streaming <see cref="Utf8JsonReader"/> walk — no <see cref="JsonDocument"/>,
/// no <see cref="JsonElement"/>, no <see cref="string"/> allocations for
/// string values. <see cref="Utf8JsonReader.CopyString(System.Span{byte})"/>
/// unescapes directly into a fresh <see cref="byte"/> array sized from the
/// reader's own length hint, so escape-decoding and UTF-8 emission are a
/// single pass.
/// </remarks>
internal static class CslJsonLoader
{
    /// <summary>Loads a CSL-JSON file from disk and returns the parsed entries.</summary>
    /// <param name="path">Path to a <c>.json</c> file containing a CSL-JSON array.</param>
    /// <returns>Parsed entries.</returns>
    public static IReadOnlyList<CitationEntry> LoadFile(FilePath path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path.Value);
        var bytes = File.ReadAllBytes(path.Value);
        return Parse(bytes);
    }

    /// <summary>Parses a CSL-JSON UTF-8 buffer into entries.</summary>
    /// <param name="json">UTF-8 source.</param>
    /// <returns>Parsed entries.</returns>
    public static IReadOnlyList<CitationEntry> Parse(in ReadOnlyMemory<byte> json)
    {
        Utf8JsonReader reader = new(json.Span, isFinalBlock: true, state: default);
        if (!reader.Read() || reader.TokenType is not JsonTokenType.StartArray)
        {
            return [];
        }

        List<CitationEntry> entries = new(64);
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            if (reader.TokenType is not JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            if (TryReadEntry(ref reader, out var entry))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>Reads one CSL-JSON object into a <see cref="CitationEntry"/>; returns <see langword="false"/> when the entry is missing its <c>id</c>.</summary>
    /// <param name="reader">Reader positioned on <see cref="JsonTokenType.StartObject"/>.</param>
    /// <param name="entry">Parsed entry on success.</param>
    /// <returns>True when an entry was produced.</returns>
    private static bool TryReadEntry(ref Utf8JsonReader reader, out CitationEntry entry)
    {
        entry = null!;
        var fields = NewEntryFields();
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOneProperty(ref reader, ref fields);
        }

        if (fields.Id.Length is 0)
        {
            return false;
        }

        entry = BuildEntry(fields);
        return true;
    }

    /// <summary>Initializes a fresh <see cref="EntryFields"/> with empty arrays for every byte-shaped field so the parser only has to overwrite hits.</summary>
    /// <returns>A zeroed field bag.</returns>
    private static EntryFields NewEntryFields() => new()
    {
        Id = [],
        Type = EntryType.Other,
        Title = [],
        ShortTitle = [],
        Authors = [],
        Editors = [],
        Year = 0,
        ContainerTitle = [],
        Publisher = [],
        PublisherPlace = [],
        Volume = [],
        Issue = [],
        Page = [],
        Url = [],
        Doi = [],
        Note = [],
        Court = [],
        Jurisdiction = [],
        LawReportSeries = [],
        MediumNeutralCitation = []
    };

    /// <summary>Materializes the parsed <see cref="EntryFields"/> bag into a final <see cref="CitationEntry"/>.</summary>
    /// <param name="fields">Parsed field bag.</param>
    /// <returns>Frozen entry.</returns>
    private static CitationEntry BuildEntry(EntryFields fields) => new()
    {
        Id = fields.Id,
        Type = fields.Type,
        Title = fields.Title,
        ShortTitle = fields.ShortTitle,
        Authors = fields.Authors,
        Editors = fields.Editors,
        Year = fields.Year,
        ContainerTitle = fields.ContainerTitle,
        Publisher = fields.Publisher,
        PublisherPlace = fields.PublisherPlace,
        Volume = fields.Volume,
        Issue = fields.Issue,
        Page = fields.Page,
        Url = fields.Url,
        Doi = fields.Doi,
        Note = fields.Note,
        Court = fields.Court,
        Jurisdiction = fields.Jurisdiction,
        LawReportSeries = fields.LawReportSeries,
        MediumNeutralCitation = fields.MediumNeutralCitation
    };

    /// <summary>Dispatches one CSL property-name token to its field reader.</summary>
    /// <param name="reader">Reader positioned on the property-name token.</param>
    /// <param name="fields">Mutable per-entry field bag.</param>
    [SuppressMessage(
        "Major Code Smell",
        "S138:Methods should not have too many lines",
        Justification = "Property-name dispatch — one branch per known CSL field, no nested logic.")]
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Property-name dispatch — cyclomatic complexity tracks the number of CSL fields, not branching logic.")]
    [SuppressMessage(
        "Sonar Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "Property-name dispatch — one branch per known CSL field, no nested logic.")]
    private static void ReadOneProperty(ref Utf8JsonReader reader, ref EntryFields fields)
    {
        if (reader.ValueTextEquals("id"u8))
        {
            fields.Id = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("type"u8))
        {
            fields.Type = ParseType(ReadStringBytes(ref reader));
            return;
        }

        if (reader.ValueTextEquals("title"u8))
        {
            fields.Title = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("title-short"u8))
        {
            fields.ShortTitle = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("author"u8))
        {
            fields.Authors = ReadNames(ref reader);
            return;
        }

        if (reader.ValueTextEquals("editor"u8))
        {
            fields.Editors = ReadNames(ref reader);
            return;
        }

        if (reader.ValueTextEquals("issued"u8))
        {
            fields.Year = ReadIssuedYear(ref reader);
            return;
        }

        if (reader.ValueTextEquals("container-title"u8))
        {
            fields.ContainerTitle = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("publisher"u8))
        {
            fields.Publisher = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("publisher-place"u8))
        {
            fields.PublisherPlace = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("volume"u8))
        {
            fields.Volume = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("issue"u8))
        {
            fields.Issue = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("page"u8))
        {
            fields.Page = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("URL"u8))
        {
            fields.Url = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("DOI"u8))
        {
            fields.Doi = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("note"u8))
        {
            fields.Note = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("authority"u8))
        {
            fields.Court = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("jurisdiction"u8))
        {
            fields.Jurisdiction = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("references"u8))
        {
            fields.LawReportSeries = ReadStringBytes(ref reader);
            return;
        }

        if (reader.ValueTextEquals("number"u8))
        {
            fields.MediumNeutralCitation = ReadStringBytes(ref reader);
            return;
        }

        reader.Read();
        reader.Skip();
    }

    /// <summary>Advances past the property-name token and returns the immediately-following string value as fresh UTF-8 bytes.</summary>
    /// <param name="reader">Reader positioned on the property-name token.</param>
    /// <returns>UTF-8 bytes; empty when the next token isn't a string.</returns>
    private static byte[] ReadStringBytes(ref Utf8JsonReader reader)
    {
        reader.Read();
        if (reader.TokenType is not JsonTokenType.String)
        {
            return [];
        }

        var maxLength = reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length;
        if (maxLength is 0)
        {
            return [];
        }

        var dst = new byte[maxLength];
        var written = reader.CopyString(dst);
        return written == dst.Length ? dst : dst[..written];
    }

    /// <summary>Reads the <c>author</c> / <c>editor</c> array into a <see cref="PersonName"/> array.</summary>
    /// <param name="reader">Reader positioned on the property-name token.</param>
    /// <returns>Parsed names; empty array when the value isn't an array.</returns>
    private static PersonName[] ReadNames(ref Utf8JsonReader reader)
    {
        reader.Read();
        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        List<PersonName> names = new(8);
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            if (reader.TokenType is not JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }

            names.Add(ReadName(ref reader));
        }

        return [.. names];
    }

    /// <summary>Reads one CSL name object.</summary>
    /// <param name="reader">Reader positioned on <see cref="JsonTokenType.StartObject"/>.</param>
    /// <returns>The parsed name.</returns>
    private static PersonName ReadName(ref Utf8JsonReader reader)
    {
        byte[] family = [];
        byte[] given = [];
        byte[] suffix = [];
        byte[] literal = [];

        while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("family"u8))
            {
                family = ReadStringBytes(ref reader);
            }
            else if (reader.ValueTextEquals("given"u8))
            {
                given = ReadStringBytes(ref reader);
            }
            else if (reader.ValueTextEquals("suffix"u8))
            {
                suffix = ReadStringBytes(ref reader);
            }
            else if (reader.ValueTextEquals("literal"u8))
            {
                literal = ReadStringBytes(ref reader);
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        return new(family, given, suffix, literal);
    }

    /// <summary>Reads <c>issued.date-parts[0][0]</c> per CSL-JSON.</summary>
    /// <param name="reader">Reader positioned on the <c>issued</c> property-name token.</param>
    /// <returns>The parsed year, or 0 when absent.</returns>
    private static int ReadIssuedYear(ref Utf8JsonReader reader)
    {
        reader.Read();
        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            reader.Skip();
            return 0;
        }

        var year = 0;
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndObject)
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("date-parts"u8))
            {
                year = ReadFirstDatePart(ref reader);
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        return year;
    }

    /// <summary>Reads the first integer of <c>date-parts[0]</c>.</summary>
    /// <param name="reader">Reader positioned on the <c>date-parts</c> property-name token.</param>
    /// <returns>The first integer, or 0 when absent / malformed.</returns>
    private static int ReadFirstDatePart(ref Utf8JsonReader reader)
    {
        reader.Read();
        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            reader.Skip();
            return 0;
        }

        if (!reader.Read() || reader.TokenType is not JsonTokenType.StartArray)
        {
            // Outer array empty or malformed.
            SkipUntilArrayEnd(ref reader);
            return 0;
        }

        var year = 0;
        if (reader.Read() && reader.TokenType is JsonTokenType.Number && reader.TryGetInt32(out var parsed))
        {
            year = parsed;
        }

        // Drain the inner array.
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            reader.Skip();
        }

        // Drain any remaining inner-array entries in the outer array.
        SkipUntilArrayEnd(ref reader);
        return year;
    }

    /// <summary>Drains tokens until the outer array's <see cref="JsonTokenType.EndArray"/> is consumed.</summary>
    /// <param name="reader">Reader.</param>
    private static void SkipUntilArrayEnd(ref Utf8JsonReader reader)
    {
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            reader.Skip();
        }
    }

    /// <summary>Maps the CSL <c>type</c> bytes to our <see cref="EntryType"/> enum.</summary>
    /// <param name="type">CSL type bytes (kebab-case).</param>
    /// <returns>The mapped <see cref="EntryType"/>; <see cref="EntryType.Other"/> when unknown.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Linear sequence-of-equals checks against UTF-8 literals; intentionally explicit per type.")]
    private static EntryType ParseType(byte[] type)
    {
        var span = (ReadOnlySpan<byte>)type;
        if (span.SequenceEqual("book"u8))
        {
            return EntryType.Book;
        }

        if (span.SequenceEqual("chapter"u8))
        {
            return EntryType.Chapter;
        }

        if (span.SequenceEqual("article-journal"u8))
        {
            return EntryType.ArticleJournal;
        }

        if (span.SequenceEqual("article-magazine"u8))
        {
            return EntryType.ArticleMagazine;
        }

        if (span.SequenceEqual("article-newspaper"u8))
        {
            return EntryType.ArticleNewspaper;
        }

        if (span.SequenceEqual("article"u8))
        {
            return EntryType.Article;
        }

        if (span.SequenceEqual("legal_case"u8))
        {
            return EntryType.LegalCase;
        }

        if (span.SequenceEqual("legislation"u8))
        {
            return EntryType.Legislation;
        }

        if (span.SequenceEqual("treaty"u8))
        {
            return EntryType.Treaty;
        }

        if (span.SequenceEqual("report"u8))
        {
            return EntryType.Report;
        }

        if (span.SequenceEqual("paper-conference"u8))
        {
            return EntryType.PaperConference;
        }

        if (span.SequenceEqual("thesis"u8))
        {
            return EntryType.Thesis;
        }

        if (span.SequenceEqual("webpage"u8))
        {
            return EntryType.Webpage;
        }

        if (span.SequenceEqual("manuscript"u8))
        {
            return EntryType.Manuscript;
        }

        return EntryType.Other;
    }

    /// <summary>Mutable per-entry field bag; passed by <c>ref</c> so the property-name dispatch helper writes through to the enclosing read scope.</summary>
    [SuppressMessage(
        "Sonar Code Smell",
        "S3898:Implement IEquatable in value type",
        Justification = "Private mutable field bag used only as a parsing scratchpad; equality is never compared.")]
    private struct EntryFields
    {
        /// <summary>Citation id bytes; required.</summary>
        public byte[] Id;

        /// <summary>CSL <c>type</c>.</summary>
        public EntryType Type;

        /// <summary>CSL <c>title</c>.</summary>
        public byte[] Title;

        /// <summary>CSL <c>title-short</c>.</summary>
        public byte[] ShortTitle;

        /// <summary>CSL <c>author</c> array.</summary>
        public PersonName[] Authors;

        /// <summary>CSL <c>editor</c> array.</summary>
        public PersonName[] Editors;

        /// <summary>CSL <c>issued.date-parts[0][0]</c>.</summary>
        public int Year;

        /// <summary>CSL <c>container-title</c>.</summary>
        public byte[] ContainerTitle;

        /// <summary>CSL <c>publisher</c>.</summary>
        public byte[] Publisher;

        /// <summary>CSL <c>publisher-place</c>.</summary>
        public byte[] PublisherPlace;

        /// <summary>CSL <c>volume</c>.</summary>
        public byte[] Volume;

        /// <summary>CSL <c>issue</c>.</summary>
        public byte[] Issue;

        /// <summary>CSL <c>page</c>.</summary>
        public byte[] Page;

        /// <summary>CSL <c>URL</c>.</summary>
        public byte[] Url;

        /// <summary>CSL <c>DOI</c>.</summary>
        public byte[] Doi;

        /// <summary>CSL <c>note</c>.</summary>
        public byte[] Note;

        /// <summary>CSL <c>authority</c> (AGLC court).</summary>
        public byte[] Court;

        /// <summary>CSL <c>jurisdiction</c>.</summary>
        public byte[] Jurisdiction;

        /// <summary>CSL <c>references</c> (AGLC law-report series).</summary>
        public byte[] LawReportSeries;

        /// <summary>CSL <c>number</c> (AGLC medium-neutral citation).</summary>
        public byte[] MediumNeutralCitation;
    }
}
