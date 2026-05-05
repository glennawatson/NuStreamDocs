// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Nav;

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Parses an mkdocs.yml document's <c>nav:</c> tree (after the YAML→JSON conversion) into a
/// dialect-neutral <see cref="NavEntry"/> array.
/// </summary>
/// <remarks>
/// Lives in the mkdocs reader assembly so dialect-specific shape parsing never leaks into core or
/// the nav module. Recursive walker — each item is either a bare string (leaf with no title) or a
/// single-key object whose value is a string (titled leaf or external link) or array (section).
/// </remarks>
public static class MkDocsNavParser
{
    /// <summary>Reads the <c>nav</c> field from <paramref name="utf8Json"/> and returns the curated tree.</summary>
    /// <param name="utf8Json">UTF-8 JSON bytes (post YAML→JSON conversion).</param>
    /// <returns>Curated entries; empty when no <c>nav:</c> block was present.</returns>
    public static NavEntry[] FromJson(ReadOnlySpan<byte> utf8Json)
    {
        Utf8JsonReader reader = new(utf8Json, isFinalBlock: true, state: default);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        return !root.TryGetProperty("nav"u8, out var nav) || nav.ValueKind != JsonValueKind.Array ? [] : ReadNavArray(nav);
    }

    /// <summary>Reads the YAML bytes of an mkdocs.yml file and returns its curated nav tree.</summary>
    /// <param name="utf8Yaml">UTF-8 YAML bytes.</param>
    /// <returns>Curated entries.</returns>
    public static NavEntry[] FromYaml(ReadOnlySpan<byte> utf8Yaml)
    {
        // Reuses the existing YAML→JSON pipeline on the way to the array reader.
        var json = ConvertYamlToJsonBytes(utf8Yaml);
        return FromJson(json);
    }

    /// <summary>Recursive nav-array reader.</summary>
    /// <param name="array">JSON array element.</param>
    /// <returns>Right-sized entry array.</returns>
    private static NavEntry[] ReadNavArray(in JsonElement array)
    {
        var capacity = array.GetArrayLength();
        if (capacity == 0)
        {
            return [];
        }

        var buffer = ArrayPool<NavEntry>.Shared.Rent(capacity);
        try
        {
            var count = 0;
            var items = array.EnumerateArray();
            while (items.MoveNext())
            {
                if (TryReadEntry(items.Current, out var entry))
                {
                    buffer[count++] = entry;
                }
            }

            return NavBuilder.ToArray(buffer, count);
        }
        finally
        {
            ArrayPool<NavEntry>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>Tries to decode one nav entry from a JSON element.</summary>
    /// <param name="item">Candidate item.</param>
    /// <param name="entry">The decoded entry on success.</param>
    /// <returns>True when the item was a recognized shape.</returns>
    private static bool TryReadEntry(in JsonElement item, out NavEntry entry)
    {
        if (item.ValueKind is JsonValueKind.String)
        {
            // Bare path — empty title means "derive from the file".
            var path = item.GetString() ?? string.Empty;
            entry = new([], EncodeUtf8(path), []);
            return path.Length > 0;
        }

        if (item.ValueKind is not JsonValueKind.Object)
        {
            entry = default;
            return false;
        }

        return TryReadDictEntry(item, out entry);
    }

    /// <summary>Tries to decode a single-key object entry (mkdocs' <c>{Title: ...}</c> shape).</summary>
    /// <param name="obj">The object element.</param>
    /// <param name="entry">The decoded entry on success.</param>
    /// <returns>True on success.</returns>
    private static bool TryReadDictEntry(in JsonElement obj, out NavEntry entry)
    {
        var props = obj.EnumerateObject();
        if (!props.MoveNext())
        {
            entry = default;
            return false;
        }

        var prop = props.Current;
        var titleBytes = EncodeUtf8(prop.Name);
        switch (prop.Value.ValueKind)
        {
            case JsonValueKind.String:
            {
                entry = new(titleBytes, EncodeUtf8(prop.Value.GetString() ?? string.Empty), []);
                return true;
            }

            case JsonValueKind.Array:
            {
                entry = new(titleBytes, [], ReadNavArray(prop.Value));
                return true;
            }

            default:
            {
                entry = default;
                return false;
            }
        }
    }

    /// <summary>Converts the YAML <paramref name="utf8Yaml"/> bytes into JSON bytes via the existing pipeline.</summary>
    /// <param name="utf8Yaml">UTF-8 YAML bytes.</param>
    /// <returns>JSON bytes.</returns>
    private static byte[] ConvertYamlToJsonBytes(ReadOnlySpan<byte> utf8Yaml)
    {
        ArrayBufferWriter<byte> rented = new();
        using Utf8JsonWriter writer = new(rented);
        YamlToJson.Convert(utf8Yaml, writer);
        writer.Flush();
        return rented.WrittenSpan.ToArray();
    }

    /// <summary>UTF-8-encodes <paramref name="value"/>; returns an empty array for null or empty input.</summary>
    /// <param name="value">Source string.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] EncodeUtf8(string? value) =>
        string.IsNullOrEmpty(value) ? [] : Encoding.UTF8.GetBytes(value);
}
