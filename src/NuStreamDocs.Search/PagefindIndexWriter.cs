// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;

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
    public static void Write(string searchRoot, SearchDocument[] documents)
    {
        ArgumentException.ThrowIfNullOrEmpty(searchRoot);
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
            var recordPath = Path.Combine(recordsDir, slug + ".json");
            WriteRecord(recordPath, doc);

            manifest.WriteStartObject();
            manifest.WriteString("slug"u8, slug);
            manifest.WriteString("url"u8, doc.RelativeUrl);
            manifest.WriteString("title"u8, (ReadOnlySpan<byte>)doc.Title);
            manifest.WriteEndObject();
        }

        manifest.WriteEndArray();
        manifest.WriteEndObject();
        manifest.Flush();
    }

    /// <summary>Writes one per-page record JSON.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="doc">Document to serialise.</param>
    private static void WriteRecord(string path, in SearchDocument doc)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("url"u8, doc.RelativeUrl);
        writer.WriteString("title"u8, (ReadOnlySpan<byte>)doc.Title);
        writer.WriteString("content"u8, (ReadOnlySpan<byte>)doc.Text);
        writer.WriteEndObject();
        writer.Flush();
    }

    /// <summary>Turns a URL into a filesystem-safe slug, falling back to an index when needed.</summary>
    /// <param name="url">Relative URL.</param>
    /// <param name="ordinal">Per-document ordinal used as a uniqueness seed.</param>
    /// <returns>Slug suitable for a record filename.</returns>
    private static string SlugifyForRecord(string url, int ordinal)
    {
        if (string.IsNullOrEmpty(url))
        {
            return Fallback(ordinal);
        }

        // string.Create + Span<char> writes the slug straight into the
        // string's storage — one allocation, no StringBuilder doubling.
        return string.Create(url.Length, url, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = IsSlugSafe(source[i]) ? source[i] : '-';
            }
        });
    }

    /// <summary>True for characters that don't need escaping in a record filename.</summary>
    /// <param name="c">Candidate char.</param>
    /// <returns>True for ASCII alphanumerics, hyphen, and underscore.</returns>
    private static bool IsSlugSafe(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_';

    /// <summary>Returns the <c>page-N</c> fallback slug.</summary>
    /// <param name="ordinal">Document ordinal.</param>
    /// <returns>Slug string.</returns>
    private static string Fallback(int ordinal) => PagefindFallbackSlug.For(ordinal);
}
