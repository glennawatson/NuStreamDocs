// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Nav.Tests;

/// <summary>Behavior tests for <c>PagesFileReader</c>.</summary>
public class PagesFileReaderTests
{
    /// <summary>A bare <c>title:</c> scalar populates <c>PagesFile.Title</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsTitle()
    {
        var parsed = Parse("title: Custom Section\n");
        await Assert.That(Encoding.UTF8.GetString(parsed.Title)).IsEqualTo("Custom Section");
        await Assert.That(parsed.Hide).IsFalse();
        await Assert.That(parsed.OrderedEntries.Length).IsEqualTo(0);
    }

    /// <summary><c>hide: true</c> sets <c>PagesFile.Hide</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsHide()
    {
        var parsed = Parse("hide: true\n");
        await Assert.That(parsed.Hide).IsTrue();
    }

    /// <summary>A <c>nav:</c> block list collects entries in source order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsBlockNav()
    {
        var parsed = Parse("nav:\n  - intro.md\n  - subsection\n  - reference.md\n");
        await Assert.That(JoinEntries(parsed.OrderedEntries)).IsEqualTo("intro.md|subsection|reference.md");
    }

    /// <summary>A combined file populates all three fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsCombinedFile()
    {
        const string Source = "title: Guide\nhide: false\nnav:\n  - intro.md\n  - advanced.md\n";
        var parsed = Parse(Source);
        await Assert.That(Encoding.UTF8.GetString(parsed.Title)).IsEqualTo("Guide");
        await Assert.That(parsed.Hide).IsFalse();
        await Assert.That(JoinEntries(parsed.OrderedEntries)).IsEqualTo("intro.md|advanced.md");
    }

    /// <summary>Empty input → <c>PagesFile.Empty</c>-equivalent shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputReturnsDefaults()
    {
        var parsed = Parse(string.Empty);
        await Assert.That(parsed.Title.Length).IsEqualTo(0);
        await Assert.That(parsed.Hide).IsFalse();
        await Assert.That(parsed.OrderedEntries.Length).IsEqualTo(0);
    }

    /// <summary>Helper to drive the parser over UTF-8 bytes.</summary>
    /// <param name="text">Source text.</param>
    /// <returns>Parsed <c>PagesFile</c>.</returns>
    private static PagesFile Parse(string text) => PagesFileReader.Parse(Encoding.UTF8.GetBytes(text));

    /// <summary>Decodes <paramref name="entries"/> and joins them with <c>|</c> for compact assertion.</summary>
    /// <param name="entries">UTF-8 entries.</param>
    /// <returns>Pipe-joined string.</returns>
    private static string JoinEntries(byte[][] entries)
    {
        var decoded = new string[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            decoded[i] = Encoding.UTF8.GetString(entries[i]);
        }

        return string.Join('|', decoded);
    }
}
