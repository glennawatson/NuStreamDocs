// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace NuStreamDocs.Config.MkDocs.Tests;

/// <summary>End-to-end tests for the <c>YamlToJson</c> converter.</summary>
public class YamlToJsonTests
{
    /// <summary>A flat key/value mapping should round-trip to a JSON object with string properties.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConvertsFlatMapping()
    {
        var json = Convert("site_name: Hello\nsite_url: https://example.test\n"u8);
        await Assert.That(json).IsEqualTo("{\"site_name\":\"Hello\",\"site_url\":\"https://example.test\"}");
    }

    /// <summary>Comment lines and trailing inline comments should be ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoresCommentLinesAndTrailing()
    {
        var json = Convert("# leading\nsite_name: A # trailing\n"u8);
        await Assert.That(json).IsEqualTo("{\"site_name\":\"A\"}");
    }

    /// <summary>A nested mapping should produce a nested JSON object.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConvertsNestedMapping()
    {
        var json = Convert("theme:\n  name: material\n  language: en\n"u8);
        await Assert.That(json).IsEqualTo("{\"theme\":{\"name\":\"material\",\"language\":\"en\"}}");
    }

    /// <summary>A block sequence of strings should produce a JSON array.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConvertsStringSequence()
    {
        var json = Convert("plugins:\n  - search\n  - blog\n"u8);
        await Assert.That(json).IsEqualTo("{\"plugins\":[\"search\",\"blog\"]}");
    }

    /// <summary>A block sequence of single-key mappings should produce an array of objects.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConvertsSequenceOfMappings()
    {
        var json = Convert("nav:\n  - Home: index.md\n  - Guide: guide.md\n"u8);
        await Assert.That(json).IsEqualTo("{\"nav\":[{\"Home\":\"index.md\"},{\"Guide\":\"guide.md\"}]}");
    }

    /// <summary>Boolean and null tokens should map to JSON literals.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConvertsBooleanAndNullTokens()
    {
        var json = Convert("flag: true\nother: false\nempty: null\ntilde: ~\n"u8);
        await Assert.That(json).IsEqualTo("{\"flag\":true,\"other\":false,\"empty\":null,\"tilde\":null}");
    }

    /// <summary>Quoted strings should keep characters that look like YAML control bytes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task QuotedStringsArePreserved()
    {
        var json = Convert("key: \"a: b # c\"\n"u8);
        await Assert.That(json).IsEqualTo("{\"key\":\"a: b # c\"}");
    }

    /// <summary>Convenience: convert and stringify.</summary>
    /// <param name="yaml">UTF-8 YAML source.</param>
    /// <returns>The compact UTF-8 JSON string.</returns>
    private static string Convert(ReadOnlySpan<byte> yaml)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            YamlToJson.Convert(yaml, writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
