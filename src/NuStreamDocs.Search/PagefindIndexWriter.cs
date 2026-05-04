// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search;

/// <summary>
/// Writes a Pagefind-compatible <c>pagefind-entry.json</c> manifest
/// plus a per-page <c>pagefind-records/{slug}.json</c> file.
/// </summary>
/// <remarks>
/// This is the v1 shape: a manifest enumerates every record-file
/// path and length, and the records hold each page's plain-text
/// body. The full Pagefind format also includes binary-encoded
/// inverted-index shards built by their Rust toolchain — those are
/// out of scope for the first ship; the JS UI can already do
/// brute-force scanning over the manifest, and we'll add shard
/// generation once the format is fully exercised end-to-end.
/// </remarks>
public static class PagefindIndexWriter
{
    /// <summary>Writes <paramref name="documents"/> as a Pagefind manifest + per-record files.</summary>
    /// <param name="searchRoot">Absolute path to the search subdirectory.</param>
    /// <param name="documents">Document corpus.</param>
    public static void Write(DirectoryPath searchRoot, SearchDocument[] documents)
    {
        ArgumentException.ThrowIfNullOrEmpty(searchRoot.Value);
        ArgumentNullException.ThrowIfNull(documents);

        var recordsDir = Path.Combine(searchRoot, "pagefind-records");
        Directory.CreateDirectory(recordsDir);

        var manifestPath = Path.Combine(searchRoot, "pagefind-entry.json");
        using var manifestStream = File.Create(manifestPath);
        using var manifest = new Utf8JsonWriter(manifestStream);

        manifest.WriteStartObject();
        manifest.WriteString("version"u8, "1.4.0");
        manifest.WritePropertyName("records"u8);
        manifest.WriteStartArray();

        for (var i = 0; i < documents.Length; i++)
        {
            var doc = documents[i];
            var slug = SlugifyForRecord(doc.RelativeUrl, i);
            var recordPath = Path.Combine(recordsDir, Encoding.UTF8.GetString(slug) + ".json");
            WriteRecord(recordPath, doc);

            manifest.WriteStartObject();
            manifest.WriteString("slug"u8, slug);
            manifest.WriteString("url"u8, (ReadOnlySpan<byte>)doc.RelativeUrl);
            manifest.WriteString("title"u8, (ReadOnlySpan<byte>)doc.Title);
            manifest.WriteEndObject();
        }

        manifest.WriteEndArray();
        manifest.WriteEndObject();
        manifest.Flush();
    }

    /// <summary>Writes one per-page record JSON.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="doc">Document to serialize.</param>
    private static void WriteRecord(string path, in SearchDocument doc)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("url"u8, (ReadOnlySpan<byte>)doc.RelativeUrl);
        writer.WriteString("title"u8, (ReadOnlySpan<byte>)doc.Title);
        writer.WriteString("content"u8, (ReadOnlySpan<byte>)doc.Text);
        writer.WriteEndObject();
        writer.Flush();
    }

    /// <summary>Turns a URL into a filesystem-safe slug as UTF-8 bytes, falling back to an index when the URL is empty.</summary>
    /// <param name="url">Relative URL bytes.</param>
    /// <param name="ordinal">Per-document ordinal used as a uniqueness seed.</param>
    /// <returns>Slug bytes suitable for a record filename.</returns>
    private static byte[] SlugifyForRecord(ReadOnlySpan<byte> url, int ordinal)
    {
        if (url.IsEmpty)
        {
            return PagefindFallbackSlug.For(ordinal);
        }

        var dst = new byte[url.Length];
        for (var i = 0; i < url.Length; i++)
        {
            dst[i] = IsSlugSafe(url[i]) ? url[i] : (byte)'-';
        }

        return dst;
    }

    /// <summary>True for bytes that don't need escaping in a record filename.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for ASCII alphanumerics, hyphen, and underscore.</returns>
    private static bool IsSlugSafe(byte b) =>
        b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'_';
}
