// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.Xrefs;

/// <summary>
/// Writes a DocFX-compatible <c>xrefmap.json</c> document covering
/// every <c>(uid, href)</c> pair in <see cref="Autorefs.AutorefsRegistry"/>.
/// </summary>
/// <remarks>
/// Schema:
/// <code language="json">
/// {
///   "baseUrl": "https://example.com/docs/",
///   "references": [
///     { "uid": "Foo.Bar", "href": "api/Foo.Bar.html" },
///     ...
///   ]
/// }
/// </code>
/// Order is sorted ordinal-by-uid so the file diffs cleanly between
/// builds.
/// </remarks>
internal static class XrefMapWriter
{
    /// <summary>Writes the snapshot to <paramref name="outputPath"/>.</summary>
    /// <param name="outputPath">Absolute path to write to.</param>
    /// <param name="baseUrl">Optional base URL embedded as the <c>baseUrl</c> field; empty omits the field.</param>
    /// <param name="entries">Snapshot from <c>AutorefsRegistry.Snapshot()</c>.</param>
    public static void Write(FilePath outputPath, byte[] baseUrl, (byte[] Id, byte[] Url)[] entries)
    {
        // Copy then sort by UID byte sequence — ordinal UTF-8 byte compare matches
        // ordinal string compare for valid UTF-8 and gives deterministic build-to-build ordering.
        (byte[] Id, byte[] Url)[] sorted = [.. entries];
        Array.Sort(sorted, static (a, b) => a.Id.AsSpan().SequenceCompareTo(b.Id.AsSpan()));

        ArrayBufferWriter<byte> sink = new(initialCapacity: 1024);
        using (Utf8JsonWriter writer = new(sink, new() { Indented = false }))
        {
            writer.WriteStartObject();

            if (baseUrl.Length > 0)
            {
                writer.WriteString("baseUrl"u8, baseUrl);
            }

            writer.WritePropertyName("references"u8);
            writer.WriteStartArray();
            for (var i = 0; i < sorted.Length; i++)
            {
                var (id, url) = sorted[i];
                writer.WriteStartObject();
                writer.WriteString("uid"u8, (ReadOnlySpan<byte>)id);
                writer.WriteString("href"u8, (ReadOnlySpan<byte>)url);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        File.WriteAllBytes(outputPath, sink.WrittenSpan);
    }
}
