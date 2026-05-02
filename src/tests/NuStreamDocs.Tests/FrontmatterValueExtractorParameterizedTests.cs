// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tests;

/// <summary>Parameterized key + value-shape tests for FrontmatterValueExtractor.AppendKeysTo.</summary>
public class FrontmatterValueExtractorParameterizedTests
{
    /// <summary>Inputs that have no frontmatter or no matching key produce empty output.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("body only")]
    [Arguments("---\nopen but no close\n")]
    [Arguments("---\ntitle: x\n---\nbody")]
    [Arguments("not\n---\nlate fence\n---\n")]
    public async Task NoMatchEmptyOutput(string source) =>
        await Assert.That(Extract(source, "missing")).IsEqualTo(string.Empty);

    /// <summary>Inline scalar values are appended.</summary>
    /// <param name="frontmatter">Frontmatter body (between fences).</param>
    /// <param name="key">Key to extract.</param>
    /// <param name="expected">Expected substring in output.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("title: Hello\n", "title", "Hello")]
    [Arguments("title: 'Quoted'\n", "title", "Quoted")]
    [Arguments("title: \"Double\"\n", "title", "Double")]
    [Arguments("name: alice\nage: 42\n", "age", "42")]
    public async Task InlineScalars(string frontmatter, string key, string expected) =>
        await Assert.That(Extract($"---\n{frontmatter}---\nbody", key)).Contains(expected);

    /// <summary>Block-list values include each entry.</summary>
    /// <param name="frontmatter">Frontmatter body.</param>
    /// <param name="key">Key to extract.</param>
    /// <param name="expectedItems">Substrings that must appear in the appended bytes.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("tags:\n  - one\n  - two\n", "tags", new[] { "one", "two" })]
    [Arguments("authors:\n  - Alice\n  - Bob\n  - Carol\n", "authors", new[] { "Alice", "Bob", "Carol" })]
    public async Task BlockLists(string frontmatter, string key, string[] expectedItems)
    {
        ArgumentNullException.ThrowIfNull(expectedItems);
        var output = Extract($"---\n{frontmatter}---\nbody", key);
        for (var i = 0; i < expectedItems.Length; i++)
        {
            await Assert.That(output).Contains(expectedItems[i]);
        }
    }

    /// <summary>Empty key array short-circuits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyKeysArrayNoOp()
    {
        var result = Extract("---\ntitle: x\n---\nbody");
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    /// <summary>Empty / null key entries are skipped without crash.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyKeyEntriesSkipped() =>
        await Assert.That(Extract("---\ntitle: x\n---\nbody", string.Empty, "title")).Contains("x");

    /// <summary>Helper that extracts <paramref name="keys"/> from <paramref name="source"/> and decodes the bytes.</summary>
    /// <param name="source">Source markdown.</param>
    /// <param name="keys">Keys to extract.</param>
    /// <returns>Concatenated extracted values.</returns>
    private static string Extract(string source, params string[] keys)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var sink = new ArrayBufferWriter<byte>(64);
        FrontmatterValueExtractor.AppendKeysTo(bytes, keys, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
