// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Tags.Tests;

/// <summary>Behavior tests for <c>TagsFrontmatterReader</c>.</summary>
public class TagsFrontmatterReaderTests
{
    /// <summary>Inline list shape (<c>tags: [a, b]</c>) is parsed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineListIsParsed() => await Assert.That(Join(Read("---\ntags: [foo, bar]\n---\nbody"))).IsEqualTo("foo|bar");

    /// <summary>Block list shape (<c>tags:\n  - a\n  - b</c>) is parsed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockListIsParsed() => await Assert.That(Join(Read("---\ntags:\n  - foo\n  - bar\n  - baz\n---\nbody"))).IsEqualTo("foo|bar|baz");

    /// <summary>Quoted tokens have their quotes stripped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QuotedTokensAreUnquoted() => await Assert.That(Join(Read("---\ntags: [\"hello world\", 'goodbye']\n---"))).IsEqualTo("hello world|goodbye");

    /// <summary>Single scalar value parses as a one-element list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleScalarParses() => await Assert.That(Join(Read("---\ntags: solo\n---"))).IsEqualTo("solo");

    /// <summary>Other frontmatter keys (e.g. <c>title:</c>) are ignored.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OtherKeysAreIgnored() => await Assert.That(Join(Read("---\ntitle: Hello\nauthor: Alice\ntags: [a]\n---"))).IsEqualTo("a");

    /// <summary>No frontmatter → empty array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoFrontmatterReturnsEmpty() => await Assert.That(Read("# heading\nbody").Length).IsEqualTo(0);

    /// <summary>No <c>tags:</c> key → empty array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoTagsKeyReturnsEmpty() => await Assert.That(Read("---\ntitle: Hello\n---").Length).IsEqualTo(0);

    /// <summary>Empty input → empty array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputReturnsEmpty() => await Assert.That(Read(string.Empty).Length).IsEqualTo(0);

    /// <summary>Drives bytes through <c>TagsFrontmatterReader.Read</c>.</summary>
    /// <param name="markdown">Source text.</param>
    /// <returns>Parsed UTF-8 tag arrays.</returns>
    private static byte[][] Read(string markdown) =>
        TagsFrontmatterReader.Read(Encoding.UTF8.GetBytes(markdown));

    /// <summary>Joins <paramref name="tags"/> with <c>|</c> for a flat assertion target — decodes each UTF-8 tag for human-readable failure messages.</summary>
    /// <param name="tags">Source UTF-8 tag arrays.</param>
    /// <returns>Pipe-delimited string representation.</returns>
    private static string Join(byte[][] tags)
    {
        var decoded = new string[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            decoded[i] = Encoding.UTF8.GetString(tags[i]);
        }

        return string.Join('|', decoded);
    }
}
