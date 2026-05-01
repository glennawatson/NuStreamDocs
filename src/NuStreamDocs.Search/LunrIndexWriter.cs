// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace NuStreamDocs.Search;

/// <summary>
/// Writes a Lunr-compatible <c>search_index.json</c> for the upstream
/// mkdocs-material bundled JS to consume.
/// </summary>
/// <remarks>
/// The actual Lunr index is built client-side by the bundled JS from
/// the <c>docs</c> array; we just emit the document corpus + config
/// stub. Title and text are written through
/// <see cref="Utf8JsonWriter"/>'s byte-span <c>WriteString</c>
/// overloads so the per-page bytes go straight from
/// <see cref="SearchDocument"/> to the file with no UTF-16
/// round-trip.
/// </remarks>
public static class LunrIndexWriter
{
    /// <summary>Writes <paramref name="documents"/> as Lunr-compatible JSON to <paramref name="path"/>.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="language">Language code emitted in the <c>config</c> block.</param>
    /// <param name="documents">Document corpus.</param>
    public static void Write(string path, string language, SearchDocument[] documents) =>
        Write(path, language, documents, []);

    /// <summary>Writes <paramref name="documents"/> as Lunr-compatible JSON, including an <c>extra_stopwords</c> array.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="language">Language code emitted in the <c>config</c> block.</param>
    /// <param name="documents">Document corpus.</param>
    /// <param name="extraStopwords">Additional stopwords to advertise in the <c>config</c> block; theme JS can layer them onto the language's default set.</param>
    public static void Write(string path, string language, SearchDocument[] documents, string[] extraStopwords)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(extraStopwords);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        writer.WritePropertyName("config"u8);
        writer.WriteStartObject();
        writer.WriteString("lang"u8, string.IsNullOrEmpty(language) ? "en" : language);
        writer.WriteString("separator"u8, @"[\s\-]+");
        if (extraStopwords.Length > 0)
        {
            writer.WritePropertyName("extra_stopwords"u8);
            writer.WriteStartArray();
            for (var i = 0; i < extraStopwords.Length; i++)
            {
                writer.WriteStringValue(extraStopwords[i]);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();

        writer.WritePropertyName("docs"u8);
        writer.WriteStartArray();
        for (var i = 0; i < documents.Length; i++)
        {
            var doc = documents[i];
            writer.WriteStartObject();
            writer.WriteString("location"u8, doc.RelativeUrl);
            writer.WriteString("title"u8, (ReadOnlySpan<byte>)doc.Title);
            writer.WriteString("text"u8, (ReadOnlySpan<byte>)doc.Text);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }
}
