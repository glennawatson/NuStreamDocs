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
    public static void Write(FilePath outputPath, UrlPath baseUrl, (ApiCompatString Id, UrlPath Url)[] entries)
    {
        var sorted = new (ApiCompatString Id, UrlPath Url)[entries.Length];
        Array.Copy(entries, sorted, entries.Length);
        Array.Sort(sorted, static (a, b) => string.CompareOrdinal(a.Id, b.Id));

        var sink = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        using (var writer = new Utf8JsonWriter(sink, new() { Indented = false }))
        {
            writer.WriteStartObject();

            if (!baseUrl.IsEmpty)
            {
                writer.WriteString("baseUrl"u8, baseUrl);
            }

            writer.WritePropertyName("references"u8);
            writer.WriteStartArray();
            for (var i = 0; i < sorted.Length; i++)
            {
                var (id, url) = sorted[i];
                writer.WriteStartObject();
                writer.WriteString("uid"u8, id);
                writer.WriteString("href"u8, url);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        File.WriteAllBytes(outputPath, sink.WrittenSpan);
    }
}
