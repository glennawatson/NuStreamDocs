// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;

namespace NuStreamDocs.Config;

/// <summary>
/// Shared YAML/TOML → UTF-8 JSON → <see cref="MkDocsConfig"/> pipeline used by
/// every <see cref="IConfigReader"/>. The format-specific bit is the converter
/// callback; the buffer-rent / json-write / parse plumbing is identical.
/// </summary>
public static class ConfigReaderJsonPipeline
{
    /// <summary>Synchronous span converter callback (e.g. <c>YamlToJson.Convert</c>).</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="json">Target JSON writer.</param>
    public delegate void SpanConverter(ReadOnlySpan<byte> source, Utf8JsonWriter json);

    /// <summary>Asynchronous stream converter callback (e.g. <c>YamlToJson.ConvertAsync</c>).</summary>
    /// <param name="source">UTF-8 source stream.</param>
    /// <param name="json">Target JSON writer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Awaitable that completes when the conversion finishes.</returns>
    public delegate Task StreamConverter(Stream source, Utf8JsonWriter json, CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="convert"/> against <paramref name="utf8Source"/> and parses the resulting JSON.</summary>
    /// <param name="utf8Source">UTF-8 source bytes.</param>
    /// <param name="convert">Format-specific span-to-JSON converter.</param>
    /// <returns>The parsed config.</returns>
    public static MkDocsConfig Read(ReadOnlySpan<byte> utf8Source, SpanConverter convert)
    {
        ArgumentNullException.ThrowIfNull(convert);

        var jsonBuffer = new ArrayBufferWriter<byte>();
        using (var jsonWriter = new Utf8JsonWriter(jsonBuffer))
        {
            convert(utf8Source, jsonWriter);
        }

        return MkDocsConfigJsonParser.FromJson(jsonBuffer.WrittenSpan);
    }

    /// <summary>Runs <paramref name="convert"/> against <paramref name="utf8Stream"/> and parses the resulting JSON.</summary>
    /// <param name="utf8Stream">UTF-8 source stream.</param>
    /// <param name="convert">Format-specific stream-to-JSON converter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed config.</returns>
    public static async Task<MkDocsConfig> ReadAsync(Stream utf8Stream, StreamConverter convert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utf8Stream);
        ArgumentNullException.ThrowIfNull(convert);

        var jsonBuffer = new ArrayBufferWriter<byte>();
        await using (var jsonWriter = new Utf8JsonWriter(jsonBuffer))
        {
            await convert(utf8Stream, jsonWriter, cancellationToken).ConfigureAwait(false);
        }

        return MkDocsConfigJsonParser.FromJson(jsonBuffer.WrittenSpan);
    }
}
