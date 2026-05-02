// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace NuStreamDocs.Config.MkDocs.Tests;

/// <summary>Parameterized YAML inputs covering YamlToJson scalar, list, and quoting branches.</summary>
public class YamlToJsonParameterizedTests
{
    /// <summary>Recognized true/false/null scalar literals map to JSON literals.</summary>
    /// <param name="literal">YAML literal.</param>
    /// <param name="expected">Expected JSON fragment for the value.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("true", "true")]
    [Arguments("false", "false")]
    [Arguments("null", "null")]
    [Arguments("~", "null")]
    public async Task LiteralScalars(string literal, string expected) =>
        await Assert.That(Convert($"k: {literal}\n")).Contains($"\"k\":{expected}");

    /// <summary>Quoted strings preserve characters that would otherwise be YAML control bytes.</summary>
    /// <param name="quoted">Quoted YAML value.</param>
    /// <param name="expected">JSON-escaped value contents.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("\"a: b\"", "a: b")]
    [Arguments("\"# not a comment\"", "# not a comment")]
    [Arguments("\"true\"", "true")]
    [Arguments("'apostrophe'", "apostrophe")]
    public async Task QuotedStringsPreserveContents(string quoted, string expected) =>
        await Assert.That(Convert($"k: {quoted}\n")).Contains($"\"k\":\"{expected}\"");

    /// <summary>Inline comments after a scalar value are stripped.</summary>
    /// <param name="suffix">Comment suffix.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(" # tail")]
    [Arguments("\t# tab-prefixed")]
    [Arguments("    # extra space")]
    public async Task InlineCommentsStripped(string suffix) =>
        await Assert.That(Convert($"k: hello{suffix}\n")).IsEqualTo("{\"k\":\"hello\"}");

    /// <summary>Different line-ending shapes parse equivalently.</summary>
    /// <param name="separator">Line separator.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    public async Task LineEndingShapes(string separator)
    {
        var json = Convert($"a: 1{separator}b: 2{separator}");
        await Assert.That(json).Contains("\"a\":\"1\"");
        await Assert.That(json).Contains("\"b\":\"2\"");
    }

    /// <summary>Helper that converts <paramref name="yaml"/> to JSON via the synchronous API.</summary>
    /// <param name="yaml">YAML source.</param>
    /// <returns>JSON output.</returns>
    private static string Convert(string yaml)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            YamlToJson.Convert(Encoding.UTF8.GetBytes(yaml), writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
