// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace NuStreamDocs.Config.Zensical.Tests;

/// <summary>Branch-coverage tests for TomlToJson.</summary>
public class TomlToJsonTests
{
    /// <summary>Empty input produces empty object.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyObject() => await Assert.That(Convert(string.Empty)).IsEqualTo("{}");

    /// <summary>String, integer, boolean, null are emitted with the right JSON kind.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScalarKinds()
    {
        var json = Convert("a = \"hi\"\nb = 42\nc = true\nd = false\ne =\nf = 'sq'\n");
        await Assert.That(json).Contains("\"a\":\"hi\"");
        await Assert.That(json).Contains("\"b\":42");
        await Assert.That(json).Contains("\"c\":true");
        await Assert.That(json).Contains("\"d\":false");
        await Assert.That(json).Contains("\"e\":null");
        await Assert.That(json).Contains("\"f\":\"sq\"");
    }

    /// <summary>Bare unquoted text becomes a string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareValueAsString() =>
        await Assert.That(Convert("a = bare\n")).Contains("\"a\":\"bare\"");

    /// <summary>Comment lines and blank lines are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CommentsAndBlanksSkipped() =>
        await Assert.That(Convert("# comment\n\na = 1\n")).Contains("\"a\":1");

    /// <summary>Trailing comment on a value is stripped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingComment() =>
        await Assert.That(Convert("a = 1 # tail comment\n")).Contains("\"a\":1");

    /// <summary>Single-segment table header opens a sub-object.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleTable() =>
        await Assert.That(Convert("[t]\nk = 1\n")).Contains("\"t\":{\"k\":1}");

    /// <summary>Dotted-table header opens nested sub-objects.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DottedTable() =>
        await Assert.That(Convert("[a.b]\nk = 1\n")).Contains("\"a\":{\"b\":{\"k\":1}}");

    /// <summary>Sequential table headers close the previous before opening the next.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SequentialTables()
    {
        var json = Convert("[a]\nx = 1\n[b]\ny = 2\n");
        await Assert.That(json).Contains("\"a\":{\"x\":1}");
        await Assert.That(json).Contains("\"b\":{\"y\":2}");
    }

    /// <summary>Header without a closing bracket is dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedHeader() =>
        await Assert.That(Convert("[unclosed\nk = 1\n")).Contains("\"k\":1");

    /// <summary>Quoted key strips the quotes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QuotedKey() =>
        await Assert.That(Convert("\"a-b\" = 1\n")).Contains("\"a-b\":1");

    /// <summary>CR/LF line endings are accepted.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CrlfLineEndings() =>
        await Assert.That(Convert("a = 1\r\nb = 2\r\n")).Contains("\"b\":2");

    /// <summary>Streams via ConvertAsync produce identical JSON.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConvertAsyncStreams()
    {
        var sink = new ArrayBufferWriter<byte>();
        await using var writer = new Utf8JsonWriter(sink);
        const string Input = "a = 1\n[t]\nk = 2\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Input));
        await TomlToJson.ConvertAsync(stream, writer, CancellationToken.None);
        await writer.FlushAsync();
        var json = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(json).Contains("\"a\":1");
        await Assert.That(json).Contains("\"t\":{\"k\":2}");
    }

    /// <summary>Helper that converts <paramref name="toml"/> via the synchronous API and returns the JSON string.</summary>
    /// <param name="toml">TOML source.</param>
    /// <returns>JSON output.</returns>
    private static string Convert(string toml)
    {
        var sink = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(sink);
        TomlToJson.Convert(Encoding.UTF8.GetBytes(toml), writer);
        writer.Flush();
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
