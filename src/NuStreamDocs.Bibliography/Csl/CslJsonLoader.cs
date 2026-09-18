// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography.Csl;

/// <summary>Loads a CSL-JSON bibliography file (the format used by pandoc, citation.js, and Zotero export).</summary>
internal static class CslJsonLoader
{
    /// <summary>Initial entry capacity.</summary>
    private const int InitialEntryCapacity = 64;

    /// <summary>Initial person capacity.</summary>
    private const int InitialPersonCapacity = 8;

    /// <summary>String-valued CSL properties paired with the per-entry field they populate.</summary>
    private static readonly (byte[] Name, StringFieldSetter Set)[] StringProperties =
    [
        ([.. "id"u8], static (ref EntryFields f, byte[] v) => f.Id = v),
        ([.. "title"u8], static (ref EntryFields f, byte[] v) => f.Title = v),
        ([.. "title-short"u8], static (ref EntryFields f, byte[] v) => f.ShortTitle = v),
        ([.. "container-title"u8], static (ref EntryFields f, byte[] v) => f.ContainerTitle = v),
        ([.. "publisher"u8], static (ref EntryFields f, byte[] v) => f.Publisher = v),
        ([.. "publisher-place"u8], static (ref EntryFields f, byte[] v) => f.PublisherPlace = v),
        ([.. "volume"u8], static (ref EntryFields f, byte[] v) => f.Volume = v),
        ([.. "issue"u8], static (ref EntryFields f, byte[] v) => f.Issue = v),
        ([.. "page"u8], static (ref EntryFields f, byte[] v) => f.Page = v),
        ([.. "URL"u8], static (ref EntryFields f, byte[] v) => f.Url = v),
        ([.. "DOI"u8], static (ref EntryFields f, byte[] v) => f.Doi = v),
        ([.. "note"u8], static (ref EntryFields f, byte[] v) => f.Note = v),
        ([.. "authority"u8], static (ref EntryFields f, byte[] v) => f.Court = v),
        ([.. "jurisdiction"u8], static (ref EntryFields f, byte[] v) => f.Jurisdiction = v),
        ([.. "references"u8], static (ref EntryFields f, byte[] v) => f.LawReportSeries = v),
        ([.. "number"u8], static (ref EntryFields f, byte[] v) => f.MediumNeutralCitation = v)
    ];

    /// <summary>CSL <c>type</c> values (kebab- / snake-case) mapped to <see cref="EntryType"/>.</summary>
    private static readonly Dictionary<byte[], EntryType> EntryTypesByName = CreateEntryTypesByName();

    /// <summary>Assigns a UTF-8 value to one string-valued field of a per-entry field bag.</summary>
    /// <param name="fields">Mutable per-entry field bag.</param>
    /// <param name="value">UTF-8 value bytes.</param>
    private delegate void StringFieldSetter(ref EntryFields fields, byte[] value);

    /// <summary>Loads a CSL-JSON file from disk and returns the parsed entries.</summary>
    /// <param name="path">Path to a <c>.json</c> file containing a CSL-JSON array.</param>
    /// <returns>Parsed entries.</returns>
    internal static IReadOnlyList<CitationEntry> LoadFile(in FilePath path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path.Value);
        var bytes = path.ReadAllBytes();
        return Parse(bytes);
    }

    /// <summary>Parses a CSL-JSON UTF-8 buffer into entries.</summary>
    /// <param name="json">UTF-8 source.</param>
    /// <returns>Parsed entries.</returns>
    internal static IReadOnlyList<CitationEntry> Parse(in ReadOnlyMemory<byte> json)
    {
        Utf8JsonReader reader = new(json.Span, true, default);
        if (!reader.Read() || reader.TokenType is not JsonTokenType.StartArray)
        {
            return [];
        }

        List<CitationEntry> entries = [with(InitialEntryCapacity)];
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

    /// <summary>Creates the recognized CSL entry types.</summary>
    /// <returns>The name-to-kind lookup.</returns>
    private static Dictionary<byte[], EntryType> CreateEntryTypesByName()
    {
        Dictionary<byte[], EntryType> values = [with(ByteArrayComparer.Instance)];
        values[[.. "book"u8]] = EntryType.Book;
        values[[.. "chapter"u8]] = EntryType.Chapter;
        values[[.. "article-journal"u8]] = EntryType.ArticleJournal;
        values[[.. "article-magazine"u8]] = EntryType.ArticleMagazine;
        values[[.. "article-newspaper"u8]] = EntryType.ArticleNewspaper;
        values[[.. "article"u8]] = EntryType.Article;
        values[[.. "legal_case"u8]] = EntryType.LegalCase;
        values[[.. "legislation"u8]] = EntryType.Legislation;
        values[[.. "treaty"u8]] = EntryType.Treaty;
        values[[.. "report"u8]] = EntryType.Report;
        values[[.. "paper-conference"u8]] = EntryType.PaperConference;
        values[[.. "thesis"u8]] = EntryType.Thesis;
        values[[.. "webpage"u8]] = EntryType.Webpage;
        values[[.. "manuscript"u8]] = EntryType.Manuscript;
        return values;
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

    /// <summary>Initializes a fresh <see cref="EntryFields"/> with empty arrays.</summary>
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
        MediumNeutralCitation = [],
    };

    /// <summary>Materializes the parsed field bag into a <see cref="CitationEntry"/>.</summary>
    /// <param name="fields">Parsed field bag.</param>
    /// <returns>The built entry.</returns>
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
        MediumNeutralCitation = fields.MediumNeutralCitation,
    };

    /// <summary>Dispatches one CSL property-name token to its field reader.</summary>
    /// <param name="reader">Reader positioned on the property-name token.</param>
    /// <param name="fields">Mutable per-entry field bag.</param>
    private static void ReadOneProperty(ref Utf8JsonReader reader, ref EntryFields fields)
    {
        for (var i = 0; i < StringProperties.Length; i++)
        {
            if (!reader.ValueTextEquals(StringProperties[i].Name))
            {
                continue;
            }

            StringProperties[i].Set(ref fields, ReadStringBytes(ref reader));
            return;
        }

        if (reader.ValueTextEquals("type"u8))
        {
            fields.Type = ParseType(ReadStringBytes(ref reader));
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

        _ = reader.Read();
        reader.Skip();
    }

    /// <summary>Reads the string value following the property-name token as UTF-8 bytes.</summary>
    /// <param name="reader">Reader positioned on the property-name token.</param>
    /// <returns>UTF-8 bytes; empty when the next token isn't a string.</returns>
    private static byte[] ReadStringBytes(ref Utf8JsonReader reader)
    {
        _ = reader.Read();
        if (reader.TokenType is not JsonTokenType.String)
        {
            return [];
        }

        var maxLength = reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length;
        if (maxLength is 0)
        {
            return [];
        }

        // Rent a working buffer for the unescape output; allocate the result once at exact size.
        // Replaces the prior `new byte[maxLength]` + `dst[..written]` pair (two allocations per
        // escaped string).
        var pooled = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            var written = reader.CopyString(pooled);
            if (written is 0)
            {
                return [];
            }

            var result = new byte[written];
            pooled.AsSpan(0, written).CopyTo(result);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
    }

    /// <summary>Reads the <c>author</c> / <c>editor</c> array into a <see cref="PersonName"/> array.</summary>
    /// <param name="reader">Reader positioned on the property-name token.</param>
    /// <returns>Parsed names; empty array when the value isn't an array.</returns>
    private static PersonName[] ReadNames(ref Utf8JsonReader reader)
    {
        _ = reader.Read();
        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        List<PersonName> names = [with(InitialPersonCapacity)];
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
                _ = reader.Read();
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
        _ = reader.Read();
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
                _ = reader.Read();
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
        _ = reader.Read();
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
    private static EntryType ParseType(byte[] type) =>
        EntryTypesByName.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(type, out var entryType)
            ? entryType
            : EntryType.Other;

    /// <summary>Mutable per-entry field bag.</summary>
    [SuppressMessage("Design", "SST2338", Justification = "The parser collects all citation fields together; they are not exclusive alternatives.")]
    private struct EntryFields
    {
        /// <summary>Gets or sets citation id bytes; required.</summary>
        public byte[] Id { get; set; }

        /// <summary>Gets or sets cSL <c>type</c>.</summary>
        public EntryType Type { get; set; }

        /// <summary>Gets or sets cSL <c>title</c>.</summary>
        public byte[] Title { get; set; }

        /// <summary>Gets or sets cSL <c>title-short</c>.</summary>
        public byte[] ShortTitle { get; set; }

        /// <summary>Gets or sets cSL <c>author</c> array.</summary>
        public PersonName[] Authors { get; set; }

        /// <summary>Gets or sets cSL <c>editor</c> array.</summary>
        public PersonName[] Editors { get; set; }

        /// <summary>Gets or sets cSL <c>issued.date-parts[0][0]</c>.</summary>
        public int Year { get; set; }

        /// <summary>Gets or sets cSL <c>container-title</c>.</summary>
        public byte[] ContainerTitle { get; set; }

        /// <summary>Gets or sets cSL <c>publisher</c>.</summary>
        public byte[] Publisher { get; set; }

        /// <summary>Gets or sets cSL <c>publisher-place</c>.</summary>
        public byte[] PublisherPlace { get; set; }

        /// <summary>Gets or sets cSL <c>volume</c>.</summary>
        public byte[] Volume { get; set; }

        /// <summary>Gets or sets cSL <c>issue</c>.</summary>
        public byte[] Issue { get; set; }

        /// <summary>Gets or sets cSL <c>page</c>.</summary>
        public byte[] Page { get; set; }

        /// <summary>Gets or sets cSL <c>URL</c>.</summary>
        public byte[] Url { get; set; }

        /// <summary>Gets or sets cSL <c>DOI</c>.</summary>
        public byte[] Doi { get; set; }

        /// <summary>Gets or sets cSL <c>note</c>.</summary>
        public byte[] Note { get; set; }

        /// <summary>Gets or sets cSL <c>authority</c> (AGLC court).</summary>
        public byte[] Court { get; set; }

        /// <summary>Gets or sets cSL <c>jurisdiction</c>.</summary>
        public byte[] Jurisdiction { get; set; }

        /// <summary>Gets or sets cSL <c>references</c> (AGLC law-report series).</summary>
        public byte[] LawReportSeries { get; set; }

        /// <summary>Gets or sets cSL <c>number</c> (AGLC medium-neutral citation).</summary>
        public byte[] MediumNeutralCitation { get; set; }
    }
}
