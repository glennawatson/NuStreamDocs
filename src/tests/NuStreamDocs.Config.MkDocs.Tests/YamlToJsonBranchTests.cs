// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace NuStreamDocs.Config.MkDocs.Tests;

/// <summary>Branch-coverage tests for YamlToJson.</summary>
public class YamlToJsonBranchTests
{
    /// <summary>Empty input produces an empty JSON object.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyEmptyObject() => await Assert.That(Convert(""u8)).IsEqualTo("{}");

    /// <summary>Numeric-looking values are emitted as strings (mkdocs YAML stays string-typed).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NumericValuesStayStrings() =>
        await Assert.That(Convert("count: 42\nzero: 0\n"u8)).Contains("\"count\":\"42\"");

    /// <summary>Single-quoted strings are unquoted.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleQuotedString() =>
        await Assert.That(Convert("k: 'hi'\n"u8)).IsEqualTo("{\"k\":\"hi\"}");

    /// <summary>Mixed CRLF line endings parse correctly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CrlfLineEndings() =>
        await Assert.That(Convert("a: 1\r\nb: 2\r\n"u8)).Contains("\"b\":\"2\"");

    /// <summary>Async streaming variant produces identical output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AsyncStream()
    {
        var buffer = new ArrayBufferWriter<byte>();
        await using var writer = new Utf8JsonWriter(buffer);
        var input = "a: 1\nb: 2\n"u8.ToArray();
        await using var stream = new MemoryStream(input);
        await YamlToJson.ConvertAsync(stream, writer, CancellationToken.None);
        await writer.FlushAsync();
        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        await Assert.That(json).Contains("\"a\":\"1\"");
    }

    /// <summary>Empty mapping value opens an empty sub-object.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyMappingValue() =>
        await Assert.That(Convert("k:\n"u8)).IsEqualTo("{\"k\":{}}");

    /// <summary>Indentation deeper than the existing level opens a sub-object.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DeepNesting()
    {
        var json = Convert("a:\n  b:\n    c: 1\n"u8);
        await Assert.That(json).Contains("\"a\":{\"b\":{");
    }

    /// <summary>Helper that runs the synchronous Convert and decodes the JSON.</summary>
    /// <param name="yaml">UTF-8 YAML.</param>
    /// <returns>JSON string.</returns>
    private static string Convert(ReadOnlySpan<byte> yaml)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            YamlToJson.Convert(yaml, writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
