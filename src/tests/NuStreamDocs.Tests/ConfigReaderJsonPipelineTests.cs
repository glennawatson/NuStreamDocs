// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using NuStreamDocs.Config;

namespace NuStreamDocs.Tests;

/// <summary>Direct tests for the shared YAML/TOML → JSON → MkDocsConfig pipeline.</summary>
public class ConfigReaderJsonPipelineTests
{
    /// <summary>The synchronous helper invokes the converter and parses the resulting JSON.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SyncReadInvokesConverterAndParsesJson()
    {
        var source = "irrelevant"u8;
        var config = ConfigReaderJsonPipeline.Read(source, WriteSiteNameJson);
        await Assert.That(config.SiteName).IsEqualTo("FromHelper");
    }

    /// <summary>Sync helper rejects a null converter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SyncReadNullConverterThrows() =>
        await Assert.That(static () => ConfigReaderJsonPipeline.Read(default, null!))
            .Throws<ArgumentNullException>();

    /// <summary>The async helper invokes the stream converter and parses the JSON.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AsyncReadInvokesConverterAndParsesJson()
    {
        await using var stream = new MemoryStream("ignored"u8.ToArray());
        var config = await ConfigReaderJsonPipeline.ReadAsync(stream, WriteSiteNameJsonAsync, CancellationToken.None);
        await Assert.That(config.SiteName).IsEqualTo("AsyncHelper");
    }

    /// <summary>Async helper rejects a null stream.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AsyncReadNullStreamThrows() =>
        await Assert.That(static () =>
            ConfigReaderJsonPipeline.ReadAsync(null!, WriteSiteNameJsonAsync, CancellationToken.None))
            .Throws<ArgumentNullException>();

    /// <summary>Async helper rejects a null converter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AsyncReadNullConverterThrows()
    {
        var stream = new MemoryStream();
        await Assert.That(async () =>
        {
            await using (stream)
            {
                await ConfigReaderJsonPipeline.ReadAsync(stream, null!, CancellationToken.None);
            }
        }).Throws<ArgumentNullException>();
    }

    /// <summary>Test span converter that emits a fixed site_name JSON document.</summary>
    /// <param name="source">Ignored source span.</param>
    /// <param name="json">Target JSON writer.</param>
    private static void WriteSiteNameJson(ReadOnlySpan<byte> source, Utf8JsonWriter json)
    {
        _ = source;
        json.WriteStartObject();
        json.WriteString("site_name", "FromHelper");
        json.WriteEndObject();
    }

    /// <summary>Test stream converter that emits a fixed site_name JSON document.</summary>
    /// <param name="source">Ignored source stream.</param>
    /// <param name="json">Target JSON writer.</param>
    /// <param name="cancellationToken">Ignored cancellation token.</param>
    /// <returns>Completed task.</returns>
    private static Task WriteSiteNameJsonAsync(Stream source, Utf8JsonWriter json, CancellationToken cancellationToken)
    {
        _ = source;
        _ = cancellationToken;
        json.WriteStartObject();
        json.WriteString("site_name", "AsyncHelper");
        json.WriteEndObject();
        return Task.CompletedTask;
    }
}
