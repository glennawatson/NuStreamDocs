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
    /// <summary>Maximum body-text bytes embedded in each manifest record's <c>excerpt</c> field.</summary>
    /// <remarks>
    /// Trade-off knob: the JS search matcher scans <c>title + url + excerpt</c>. Larger excerpts
    /// catch more body-only mentions at the cost of manifest size. 512 bytes per page yields a
    /// ~7 MB manifest on a 14 K-page corpus — small enough for a one-shot fetch, big enough that
    /// the first paragraph of every page participates in matching.
    /// </remarks>
    private const int ManifestExcerptBytes = 512;

    /// <summary>UTF-8 continuation-byte mask: bytes matching <c>10xxxxxx</c> are mid-codepoint and unsafe to truncate at.</summary>
    private const int Utf8ContinuationMask = 0xC0;

    /// <summary>UTF-8 continuation-byte signature (the lead two bits of a continuation byte).</summary>
    private const int Utf8ContinuationSignature = 0x80;

    /// <summary>Writes <paramref name="documents"/> as a Pagefind manifest + per-record files.</summary>
    /// <param name="searchRoot">Absolute path to the search subdirectory.</param>
    /// <param name="documents">Document corpus.</param>
    public static void Write(DirectoryPath searchRoot, SearchDocument[] documents)
    {
        ArgumentException.ThrowIfNullOrEmpty(searchRoot.Value);
        ArgumentNullException.ThrowIfNull(documents);

        DirectoryPath recordsDir = Path.Combine(searchRoot.Value, "pagefind-records");
        recordsDir.Create();

        var manifestPath = searchRoot.File("pagefind-entry.json");
        using var manifestStream = File.Create(manifestPath.Value);
        using Utf8JsonWriter manifest = new(manifestStream);

        manifest.WriteStartObject();
        manifest.WriteString("version"u8, "1.4.0");
        manifest.WritePropertyName("records"u8);
        manifest.WriteStartArray();

        for (var i = 0; i < documents.Length; i++)
        {
            var doc = documents[i];
            var slug = SlugifyForRecord(doc.RelativeUrl, i);
            var recordPath = recordsDir.File(Encoding.UTF8.GetString(slug) + ".json");
            WriteRecord(recordPath, doc);

            manifest.WriteStartObject();
            manifest.WriteString("slug"u8, slug);
            manifest.WriteString("url"u8, (ReadOnlySpan<byte>)doc.RelativeUrl);
            manifest.WriteString("title"u8, (ReadOnlySpan<byte>)doc.Title);
            manifest.WriteString("excerpt"u8, ExcerptBytes(doc.Text));
            manifest.WriteEndObject();
        }

        manifest.WriteEndArray();
        manifest.WriteEndObject();
        manifest.Flush();
    }

    /// <summary>Returns the first <see cref="ManifestExcerptBytes"/> bytes of <paramref name="text"/>, snapped to a UTF-8 boundary so we never split a multi-byte sequence.</summary>
    /// <param name="text">Full body text bytes.</param>
    /// <returns>A safe-truncated slice of <paramref name="text"/>.</returns>
    private static ReadOnlySpan<byte> ExcerptBytes(byte[] text)
    {
        if (text is null || text.Length <= ManifestExcerptBytes)
        {
            return text;
        }

        // Walk back from the cap to the start of the most recent UTF-8 codepoint so the
        // excerpt is always a valid UTF-8 string (continuation bytes 10xxxxxx are skipped).
        var end = ManifestExcerptBytes;
        while (end > 0 && (text[end] & Utf8ContinuationMask) is Utf8ContinuationSignature)
        {
            end--;
        }

        return text.AsSpan(0, end);
    }

    /// <summary>Writes one per-page record JSON.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="doc">Document to serialize.</param>
    private static void WriteRecord(FilePath path, in SearchDocument doc)
    {
        using var stream = File.Create(path.Value);
        using Utf8JsonWriter writer = new(stream);
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
