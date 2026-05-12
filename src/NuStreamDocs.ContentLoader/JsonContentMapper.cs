// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;
using NuStreamDocs.ContentLoader.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader;

/// <summary>
/// Streams a JSON document (or JSON converted from YAML) into <see cref="SyntheticPage"/> entries
/// with a forward-only <see cref="Utf8JsonReader"/> — byte-first, no document model. Each entry is
/// walked once: frontmatter and body bytes are written straight into reused buffers and placeholder
/// values are captured on the fly, then the route is rendered from the parsed template.
/// </summary>
internal static class JsonContentMapper
{
    /// <summary>Maps <paramref name="json"/> through <paramref name="mapping"/>.</summary>
    /// <param name="json">UTF-8 JSON bytes.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="loaderName">Loader name for diagnostics.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The produced pages, in document order.</returns>
    /// <exception cref="ContentLoaderException">When the JSON is malformed or the route template is unbalanced.</exception>
    public static SyntheticPage[] Map(byte[] json, ContentMapping mapping, ReadOnlySpan<byte> loaderName, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(mapping);
        mapping.Validate();

        var template = RouteTemplate.Parse(mapping.RouteTemplate)
            ?? throw new ContentLoaderException("Route template has an unclosed '{' placeholder.");
        var name = Encoding.UTF8.GetString(loaderName);
        var routeTemplateText = Encoding.UTF8.GetString(mapping.RouteTemplate);

        List<SyntheticPage> pages = [];
        try
        {
            Utf8JsonReader reader = new(json);
            if (!AdvanceToCollection(ref reader, mapping.CollectionPointer))
            {
                ContentLoaderLoggingHelper.LogCollectionPointerMissed(logger, name, Encoding.UTF8.GetString(mapping.CollectionPointer));
                return [];
            }

            RenderBuffers buffers = new(template.PlaceholderCount);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                var start = (int)reader.TokenStartIndex;
                reader.Skip();
                var entrySpan = json.AsSpan(start, (int)reader.BytesConsumed - start);
                if (TryBuildPage(entrySpan, template, mapping, buffers, out var page))
                {
                    pages.Add(page);
                }
                else
                {
                    ContentLoaderLoggingHelper.LogSkippedEntry(logger, name, routeTemplateText);
                }
            }
        }
        catch (JsonException ex)
        {
            throw new ContentLoaderException("The source is not valid JSON.", ex);
        }

        return [.. pages];
    }

    /// <summary>Advances <paramref name="reader"/> to the start of the collection array (the root, or via the dotted pointer).</summary>
    /// <param name="reader">Reader at the document start.</param>
    /// <param name="pointer">Dotted object-property path; empty means the root is the array.</param>
    /// <returns><see langword="true"/> when the path resolves to a JSON array.</returns>
    private static bool AdvanceToCollection(ref Utf8JsonReader reader, byte[] pointer)
    {
        if (!reader.Read())
        {
            return false;
        }

        if (pointer.Length == 0)
        {
            return reader.TokenType == JsonTokenType.StartArray;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }

        var i = 0;
        while (i < pointer.Length)
        {
            var dot = Array.IndexOf(pointer, (byte)'.', i);
            var end = dot < 0 ? pointer.Length : dot;
            if (!AdvanceToProperty(ref reader, pointer.AsSpan(i, end - i)))
            {
                return false;
            }

            if (dot < 0)
            {
                return reader.TokenType == JsonTokenType.StartArray;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            i = end + 1;
        }

        return false;
    }

    /// <summary>From a reader at a <c>{</c>, advances to the value of the property named <paramref name="name"/>.</summary>
    /// <param name="reader">Reader at a <c>StartObject</c>.</param>
    /// <param name="name">Property name to find.</param>
    /// <returns><see langword="true"/> when found; the reader is then on the property's value.</returns>
    private static bool AdvanceToProperty(ref Utf8JsonReader reader, ReadOnlySpan<byte> name)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var match = reader.ValueTextEquals(name);
            if (!reader.Read())
            {
                return false;
            }

            if (match)
            {
                return true;
            }

            reader.Skip();
        }

        return false;
    }

    /// <summary>Walks one entry object once, capturing the route placeholders, frontmatter pairs, and body, then assembles the page.</summary>
    /// <param name="entrySpan">UTF-8 bytes of one entry object.</param>
    /// <param name="template">Parsed route template.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="buffers">Reused output buffers.</param>
    /// <param name="page">On success, the built page.</param>
    /// <returns><see langword="true"/> when every route placeholder resolved to a scalar.</returns>
    private static bool TryBuildPage(ReadOnlySpan<byte> entrySpan, RouteTemplate template, ContentMapping mapping, RenderBuffers buffers, out SyntheticPage page)
    {
        page = default;
        buffers.Reset();
        ScanEntry(entrySpan, template, mapping, buffers);

        if (!template.TryRender(buffers, buffers.Route))
        {
            return false;
        }

        var markdown = buffers.Markdown;
        markdown.Write("---\n"u8);
        markdown.Write(buffers.Frontmatter.WrittenSpan);
        markdown.Write("---\n\n"u8);
        markdown.Write(buffers.Body.WrittenSpan);
        EnsureTrailingNewline(markdown);
        page = new(new FilePath(Encoding.UTF8.GetString(buffers.Route.WrittenSpan)), markdown.WrittenSpan.ToArray());
        return true;
    }

    /// <summary>Streams the entry's properties, routing each to the placeholder store, the body buffer, or the frontmatter buffer.</summary>
    /// <param name="entrySpan">UTF-8 bytes of one entry object.</param>
    /// <param name="template">Parsed route template.</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="buffers">Reused output buffers.</param>
    private static void ScanEntry(ReadOnlySpan<byte> entrySpan, RouteTemplate template, ContentMapping mapping, RenderBuffers buffers)
    {
        Utf8JsonReader reader = new(entrySpan);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var slot = template.FindSlot(ref reader);
            var isBody = mapping.BodyKey is [_, ..] && reader.ValueTextEquals(mapping.BodyKey);
            var inFrontmatter = !isBody && IsWhitelisted(mapping.FrontmatterKeys, ref reader);
            var nameBytes = reader.ValueSpan;
            if (!reader.Read())
            {
                return;
            }

            ConsumeValue(ref reader, entrySpan, nameBytes, slot, isBody, inFrontmatter, buffers);
        }
    }

    /// <summary>Captures the current property value into the placeholder store / body / frontmatter as directed, then advances past it.</summary>
    /// <param name="reader">Reader positioned on a property value.</param>
    /// <param name="entrySpan">UTF-8 bytes of the entry object the reader is over.</param>
    /// <param name="nameBytes">The property name bytes (a slice of <paramref name="entrySpan"/>).</param>
    /// <param name="slot">Placeholder slot for this property, or -1.</param>
    /// <param name="isBody">Whether this property is the body field.</param>
    /// <param name="inFrontmatter">Whether this property belongs in frontmatter.</param>
    /// <param name="buffers">Reused output buffers.</param>
    private static void ConsumeValue(
        scoped ref Utf8JsonReader reader,
        scoped ReadOnlySpan<byte> entrySpan,
        scoped ReadOnlySpan<byte> nameBytes,
        int slot,
        bool isBody,
        bool inFrontmatter,
        RenderBuffers buffers)
    {
        if (slot >= 0)
        {
            CaptureScalarSlot(ref reader, slot, buffers);
        }

        if (isBody)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                CopyUnescaped(ref reader, buffers.Body);
            }
        }
        else if (inFrontmatter)
        {
            buffers.Frontmatter.Write(nameBytes);
            buffers.Frontmatter.Write(": "u8);
            var valueStart = (int)reader.TokenStartIndex;
            reader.Skip();
            buffers.Frontmatter.Write(entrySpan[valueStart..(int)reader.BytesConsumed]);
            buffers.Frontmatter.Write("\n"u8);
            return;
        }

        reader.Skip();
    }

    /// <summary>True when the property the reader is on matches the frontmatter whitelist (an empty whitelist matches everything).</summary>
    /// <param name="whitelist">Allowed frontmatter field names.</param>
    /// <param name="reader">Reader positioned on a property name.</param>
    /// <returns><see langword="true"/> when the property should flow into frontmatter.</returns>
    private static bool IsWhitelisted(byte[][] whitelist, scoped ref Utf8JsonReader reader)
    {
        if (whitelist is [])
        {
            return true;
        }

        for (var i = 0; i < whitelist.Length; i++)
        {
            if (reader.ValueTextEquals(whitelist[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Captures the current value into placeholder slot <paramref name="slot"/> when it is a usable
    /// scalar — the content of an unescaped string, or the literal of a number / boolean.
    /// </summary>
    /// <param name="reader">Reader positioned on a value token.</param>
    /// <param name="slot">Placeholder slot.</param>
    /// <param name="buffers">Buffers holding the placeholder store.</param>
    private static void CaptureScalarSlot(scoped ref Utf8JsonReader reader, int slot, RenderBuffers buffers)
    {
        var isScalar = reader.TokenType is JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False
            || (reader.TokenType == JsonTokenType.String && !reader.ValueIsEscaped);
        if (!isScalar)
        {
            return;
        }

        buffers.CaptureSlot(slot, reader.ValueSpan);
    }

    /// <summary>Copies the unescaped UTF-8 bytes of the current string token to <paramref name="writer"/>.</summary>
    /// <param name="reader">Reader positioned on a string token.</param>
    /// <param name="writer">Destination.</param>
    private static void CopyUnescaped(scoped ref Utf8JsonReader reader, ArrayBufferWriter<byte> writer)
    {
        var destination = writer.GetSpan(reader.ValueSpan.Length);
        var written = reader.CopyString(destination);
        writer.Advance(written);
    }

    /// <summary>Appends a newline to <paramref name="writer"/> unless it already ends with one.</summary>
    /// <param name="writer">Destination.</param>
    private static void EnsureTrailingNewline(ArrayBufferWriter<byte> writer)
    {
        if (writer.WrittenSpan is [.., var last] && last == (byte)'\n')
        {
            return;
        }

        writer.Write("\n"u8);
    }

    /// <summary>One literal run or one <c>{field}</c> placeholder of a parsed route template.</summary>
    /// <param name="IsPlaceholder">True for a placeholder; false for a literal run.</param>
    /// <param name="Bytes">The literal bytes (literal part) or the referenced field name (placeholder part).</param>
    /// <param name="Slot">For a placeholder, the index into the placeholder store; -1 for a literal.</param>
    private readonly record struct RoutePart(bool IsPlaceholder, byte[] Bytes, int Slot);

    /// <summary>A route template parsed into ordered literal / placeholder parts plus the distinct placeholder field names.</summary>
    private sealed class RouteTemplate
    {
        /// <summary>Ordered parts of the template.</summary>
        private readonly RoutePart[] _parts;

        /// <summary>Distinct placeholder field names, indexed by slot.</summary>
        private readonly byte[][] _fieldNames;

        /// <summary>Initializes a new instance of the <see cref="RouteTemplate"/> class.</summary>
        /// <param name="parts">Ordered parts.</param>
        /// <param name="fieldNames">Distinct placeholder field names.</param>
        private RouteTemplate(RoutePart[] parts, byte[][] fieldNames)
        {
            _parts = parts;
            _fieldNames = fieldNames;
        }

        /// <summary>Gets the number of distinct placeholder fields.</summary>
        public int PlaceholderCount => _fieldNames.Length;

        /// <summary>Parses <paramref name="template"/> into ordered parts, or returns null when a <c>{</c> is unclosed.</summary>
        /// <param name="template">Route template bytes.</param>
        /// <returns>The parsed template, or null.</returns>
        public static RouteTemplate? Parse(byte[] template)
        {
            List<RoutePart> parts = [];
            List<byte[]> fieldNames = [];
            var i = 0;
            while (i < template.Length)
            {
                var open = Array.IndexOf(template, (byte)'{', i);
                if (open < 0)
                {
                    parts.Add(new(IsPlaceholder: false, template[i..], Slot: -1));
                    break;
                }

                if (open > i)
                {
                    parts.Add(new(IsPlaceholder: false, template[i..open], Slot: -1));
                }

                var close = Array.IndexOf(template, (byte)'}', open + 1);
                if (close < 0)
                {
                    return null;
                }

                var fieldName = template[(open + 1)..close];
                parts.Add(new(IsPlaceholder: true, fieldName, SlotFor(fieldNames, fieldName)));
                i = close + 1;
            }

            return new([.. parts], [.. fieldNames]);
        }

        /// <summary>Returns the placeholder slot of the property the reader is positioned on, or -1.</summary>
        /// <param name="reader">Reader positioned on a property name.</param>
        /// <returns>The slot, or -1.</returns>
        public int FindSlot(scoped ref Utf8JsonReader reader)
        {
            for (var i = 0; i < _fieldNames.Length; i++)
            {
                if (reader.ValueTextEquals(_fieldNames[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Renders the route into <paramref name="writer"/> from the captured placeholder values in <paramref name="buffers"/>.</summary>
        /// <param name="buffers">Buffers holding the captured placeholder values.</param>
        /// <param name="writer">Destination for the route bytes.</param>
        /// <returns><see langword="true"/> when every placeholder was captured.</returns>
        public bool TryRender(RenderBuffers buffers, ArrayBufferWriter<byte> writer)
        {
            for (var i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                if (!part.IsPlaceholder)
                {
                    writer.Write(part.Bytes);
                    continue;
                }

                if (!buffers.TryWriteSlot(part.Slot, writer))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Finds or appends <paramref name="fieldName"/> in <paramref name="fieldNames"/> and returns its slot.</summary>
        /// <param name="fieldNames">Accumulating list of distinct field names.</param>
        /// <param name="fieldName">Field name to locate.</param>
        /// <returns>The slot index.</returns>
        private static int SlotFor(List<byte[]> fieldNames, byte[] fieldName)
        {
            for (var i = 0; i < fieldNames.Count; i++)
            {
                if (fieldName.AsSpan().SequenceEqual(fieldNames[i]))
                {
                    return i;
                }
            }

            fieldNames.Add(fieldName);
            return fieldNames.Count - 1;
        }
    }

    /// <summary>Reused per-entry output buffers — Markdown, frontmatter, body, route, and a packed placeholder-value store.</summary>
    private sealed class RenderBuffers
    {
        /// <summary>Initial Markdown buffer capacity; pages are typically a few hundred bytes.</summary>
        private const int InitialMarkdownCapacity = 256;

        /// <summary>Packed placeholder-value bytes; <see cref="_slots"/> indexes into this.</summary>
        private readonly ArrayBufferWriter<byte> _placeholderStore = new();

        /// <summary>(start, length) into <see cref="_placeholderStore"/> per placeholder slot; start = -1 means uncaptured.</summary>
        private readonly (int Start, int Length)[] _slots;

        /// <summary>Initializes a new instance of the <see cref="RenderBuffers"/> class.</summary>
        /// <param name="placeholderCount">Number of placeholder slots to track.</param>
        public RenderBuffers(int placeholderCount) => _slots = new (int, int)[placeholderCount];

        /// <summary>Gets the Markdown output buffer.</summary>
        public ArrayBufferWriter<byte> Markdown { get; } = new(InitialMarkdownCapacity);

        /// <summary>Gets the frontmatter output buffer.</summary>
        public ArrayBufferWriter<byte> Frontmatter { get; } = new();

        /// <summary>Gets the body output buffer.</summary>
        public ArrayBufferWriter<byte> Body { get; } = new();

        /// <summary>Gets the route output buffer.</summary>
        public ArrayBufferWriter<byte> Route { get; } = new();

        /// <summary>Clears all buffers for the next entry.</summary>
        public void Reset()
        {
            Markdown.ResetWrittenCount();
            Frontmatter.ResetWrittenCount();
            Body.ResetWrittenCount();
            Route.ResetWrittenCount();
            _placeholderStore.ResetWrittenCount();
            Array.Fill(_slots, (-1, 0));
        }

        /// <summary>Records the bytes for placeholder slot <paramref name="slot"/>.</summary>
        /// <param name="slot">Placeholder slot.</param>
        /// <param name="value">Value bytes.</param>
        public void CaptureSlot(int slot, ReadOnlySpan<byte> value)
        {
            var start = _placeholderStore.WrittenCount;
            _placeholderStore.Write(value);
            _slots[slot] = (start, value.Length);
        }

        /// <summary>Writes the captured bytes for placeholder slot <paramref name="slot"/> to <paramref name="writer"/>.</summary>
        /// <param name="slot">Placeholder slot.</param>
        /// <param name="writer">Destination.</param>
        /// <returns><see langword="true"/> when the slot was captured.</returns>
        public bool TryWriteSlot(int slot, ArrayBufferWriter<byte> writer)
        {
            var (start, length) = _slots[slot];
            if (start < 0)
            {
                return false;
            }

            writer.Write(_placeholderStore.WrittenSpan.Slice(start, length));
            return true;
        }
    }
}
