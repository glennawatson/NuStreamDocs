// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Lunr;

/// <summary>Writes a Lunr-compatible <c>search_index.json</c>.</summary>
public static class LunrIndexWriter
{
    /// <summary>JSON writer options reused across emits.</summary>
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        SkipValidation = true
    };

    /// <summary>Writes <paramref name="documents"/> as Lunr-compatible JSON to <paramref name="path"/>.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="language">UTF-8 language code emitted in the <c>config</c> block; empty falls back to <c>en</c>.</param>
    /// <param name="documents">Document corpus.</param>
    public static void Write(in FilePath path, ReadOnlySpan<byte> language, SearchDocument[] documents) =>
        Write(path, language, documents, []);

    /// <summary>Writes <paramref name="documents"/> as Lunr-compatible JSON, including an <c>extra_stopwords</c> array.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="language">UTF-8 language code emitted in the <c>config</c> block; empty falls back to <c>en</c>.</param>
    /// <param name="documents">Document corpus.</param>
    /// <param name="extraStopwords">UTF-8 stopwords advertised in the <c>config</c> block.</param>
    public static void Write(in FilePath path, ReadOnlySpan<byte> language, SearchDocument[] documents, byte[][] extraStopwords)
    {
        ArgumentException.ThrowIfNullOrEmpty(path.Value);

        path.Directory.Create();
        using var stream = File.Create(path.Value);
        using Utf8JsonWriter writer = new(stream, WriterOptions);

        writer.WriteStartObject();

        writer.WritePropertyName("config"u8);
        writer.WriteStartObject();
        writer.WriteString("lang"u8, language.IsEmpty ? "en"u8 : language);
        writer.WriteString("separator"u8, @"[\s\-]+"u8);
        if (extraStopwords.Length > 0)
        {
            writer.WritePropertyName("extra_stopwords"u8);
            writer.WriteStartArray();
            for (var i = 0; i < extraStopwords.Length; i++)
            {
                writer.WriteStringValue((ReadOnlySpan<byte>)extraStopwords[i]);
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
            writer.WriteString("location"u8, (ReadOnlySpan<byte>)doc.RelativeUrl);
            writer.WriteString("title"u8, (ReadOnlySpan<byte>)doc.Title);
            writer.WriteString("text"u8, (ReadOnlySpan<byte>)doc.Text);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }
}
