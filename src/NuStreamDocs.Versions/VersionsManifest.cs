// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.Versions;

/// <summary>
/// Reads and writes <c>versions.json</c> in the mike-compatible shape:
/// a JSON array of <c>{ version, title, aliases }</c> objects.
/// </summary>
/// <remarks>
/// Hand-written <see cref="Utf8JsonReader"/> + <see cref="Utf8JsonWriter"/>
/// loop so the file path stays AOT-clean without a source-generated
/// serializer. Reads are tolerant of additional unknown properties
/// (mike's docVersion field, etc) — they're skipped.
/// </remarks>
public static class VersionsManifest
{
    /// <summary>Gets the on-disk filename. Matches mike's convention.</summary>
    public static string FileName => "versions.json";

    /// <summary>Reads the manifest at <paramref name="parentDir"/>/<see cref="FileName"/>.</summary>
    /// <param name="parentDir">Parent site directory containing every version's subdirectory.</param>
    /// <returns>The parsed entries; empty when the file is missing.</returns>
    public static VersionEntry[] Read(DirectoryPath parentDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDir);
        return ReadCore(parentDir);
    }

    /// <summary>Parses a JSON array of version entries from UTF-8 bytes.</summary>
    /// <param name="bytes">UTF-8 source.</param>
    /// <returns>The parsed entries.</returns>
    public static VersionEntry[] ReadFromUtf8(ReadOnlySpan<byte> bytes)
    {
        List<VersionEntry> entries = new(8);
        Utf8JsonReader reader = new(bytes, isFinalBlock: true, state: default);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return [];
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
        {
            entries.Add(ReadEntry(ref reader));
        }

        return [.. entries];
    }

    /// <summary>Writes the manifest at <paramref name="parentDir"/>/<see cref="FileName"/>.</summary>
    /// <param name="parentDir">Parent site directory.</param>
    /// <param name="entries">Entries to persist.</param>
    public static void Write(DirectoryPath parentDir, VersionEntry[] entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDir.Value);
        ArgumentNullException.ThrowIfNull(entries);
        Directory.CreateDirectory(parentDir);

        ArrayBufferWriter<byte> sink = new(256);
        WriteToUtf8(entries, sink);
        File.WriteAllBytes(Path.Combine(parentDir, FileName), sink.WrittenSpan);
    }

    /// <summary>Serializes the entries into <paramref name="sink"/> as a JSON array.</summary>
    /// <param name="entries">Entries to serialize.</param>
    /// <param name="sink">Buffer writer.</param>
    public static void WriteToUtf8(VersionEntry[] entries, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(sink);
        using Utf8JsonWriter writer = new(sink, new() { Indented = true });
        writer.WriteStartArray();
        for (var i = 0; i < entries.Length; i++)
        {
            WriteEntry(writer, entries[i]);
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    /// <summary>Returns the entries with <paramref name="entry"/> upserted by version.</summary>
    /// <param name="existing">Existing entries.</param>
    /// <param name="entry">New or updated entry.</param>
    /// <returns>Merged entries; ordering preserved with new entries appended.</returns>
    public static VersionEntry[] Upsert(VersionEntry[] existing, in VersionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Version);

        List<VersionEntry> merged = new(existing.Length + 1);
        var replaced = false;
        for (var i = 0; i < existing.Length; i++)
        {
            if (!replaced && string.Equals(existing[i].Version, entry.Version, StringComparison.Ordinal))
            {
                merged.Add(entry);
                replaced = true;
                continue;
            }

            merged.Add(existing[i]);
        }

        if (!replaced)
        {
            merged.Add(entry);
        }

        return [.. merged];
    }

    /// <summary>Reads one <c>{ version, title, aliases }</c> object positioned at its <c>StartObject</c>.</summary>
    /// <param name="reader">Reader positioned at the entry's open brace.</param>
    /// <returns>The parsed entry.</returns>
    private static VersionEntry ReadEntry(ref Utf8JsonReader reader)
    {
        var version = string.Empty;
        var title = string.Empty;
        byte[][] aliases = [];

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("version"u8))
            {
                reader.Read();
                version = reader.GetString() ?? string.Empty;
            }
            else if (reader.ValueTextEquals("title"u8))
            {
                reader.Read();
                title = reader.GetString() ?? string.Empty;
            }
            else if (reader.ValueTextEquals("aliases"u8))
            {
                aliases = reader.ReadStringArrayAsBytes();
            }
            else
            {
                reader.Read();
                reader.Skip();
            }
        }

        return new(version, title, aliases);
    }

    /// <summary>Writes one entry as a <c>{ version, title, aliases }</c> object.</summary>
    /// <param name="writer">Json writer.</param>
    /// <param name="entry">Entry to serialize.</param>
    private static void WriteEntry(Utf8JsonWriter writer, in VersionEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("version"u8, entry.Version);
        writer.WriteString("title"u8, entry.Title);
        writer.WritePropertyName("aliases"u8);
        writer.WriteStartArray();
        var aliases = entry.Aliases;
        for (var i = 0; i < aliases.Length; i++)
        {
            writer.WriteStringValue(aliases[i]);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Loads the manifest bytes from disk and delegates to the UTF-8 parser.</summary>
    /// <param name="parentDir">Validated parent directory.</param>
    /// <returns>The parsed entries; empty when the file is missing.</returns>
    private static VersionEntry[] ReadCore(string parentDir)
    {
        var path = Path.Combine(parentDir, FileName);
        if (!File.Exists(path))
        {
            return [];
        }

        var bytes = ((FilePath)path).ReadAllBytes();
        return ReadFromUtf8(bytes);
    }
}
