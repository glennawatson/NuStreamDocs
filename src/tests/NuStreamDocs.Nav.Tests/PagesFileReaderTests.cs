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

    /// <summary>Awesome-pages map form (<c>- Title: path</c>) extracts both halves.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsAwesomePagesTitleMap()
    {
        var parsed = Parse("nav:\n  - Home: index.md\n  - Aerodromes: aerodromes\n  - bare.md\n");
        await Assert.That(parsed.OrderedEntries.Length).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[0].Path)).IsEqualTo("index.md");
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[0].Title)).IsEqualTo("Home");
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[1].Path)).IsEqualTo("aerodromes");
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[1].Title)).IsEqualTo("Aerodromes");
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[2].Path)).IsEqualTo("bare.md");
        await Assert.That(parsed.OrderedEntries[2].Title.Length).IsEqualTo(0);
    }

    /// <summary>Quoted titles in awesome-pages entries are unwrapped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsQuotedTitleInAwesomePagesEntry()
    {
        var parsed = Parse("nav:\n  - \"Home Page\": index.md\n");
        await Assert.That(parsed.OrderedEntries.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[0].Path)).IsEqualTo("index.md");
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[0].Title)).IsEqualTo("Home Page");
    }

    /// <summary>An empty path after the colon is treated as a bare entry rather than a partial title-map.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyPathAfterColonFallsBackToWholeEntry()
    {
        var parsed = Parse("nav:\n  - bare-with-colon:\n");
        await Assert.That(parsed.OrderedEntries.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(parsed.OrderedEntries[0].Path)).IsEqualTo("bare-with-colon:");
        await Assert.That(parsed.OrderedEntries[0].Title.Length).IsEqualTo(0);
    }

    /// <summary>Indentation under <c>nav:</c> doesn't matter — block-list scanning only checks the first non-whitespace byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndentedListEntriesAreCollected()
    {
        var parsed = Parse("nav:\n      - intro.md\n      - subsection\n");
        await Assert.That(JoinEntries(parsed.OrderedEntries)).IsEqualTo("intro.md|subsection");
    }

    /// <summary><c>order: desc</c> (or <c>descending</c>) sets <c>ReverseOrder</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OrderDescSetsReverseOrder()
    {
        await Assert.That(Parse("order: desc\n").ReverseOrder).IsTrue();
        await Assert.That(Parse("order: descending\n").ReverseOrder).IsTrue();
    }

    /// <summary><c>order: asc</c>, an unknown value, or no <c>order:</c> key leaves <c>ReverseOrder</c> false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonDescOrderLeavesReverseOrderFalse()
    {
        await Assert.That(Parse("order: asc\n").ReverseOrder).IsFalse();
        await Assert.That(Parse("order: whatever\n").ReverseOrder).IsFalse();
        await Assert.That(Parse("title: Section\n").ReverseOrder).IsFalse();
    }

    /// <summary>Helper to drive the parser over UTF-8 bytes.</summary>
    /// <param name="text">Source text.</param>
    /// <returns>Parsed <c>PagesFile</c>.</returns>
    private static PagesFile Parse(string text) => PagesFileReader.Parse(Encoding.UTF8.GetBytes(text));

    /// <summary>Decodes <paramref name="entries"/> and joins paths with <c>|</c> for compact assertion.</summary>
    /// <param name="entries">Parsed entries.</param>
    /// <returns>Pipe-joined string.</returns>
    private static string JoinEntries(PagesEntry[] entries)
    {
        var decoded = new string[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            decoded[i] = Encoding.UTF8.GetString(entries[i].Path);
        }

        return string.Join('|', decoded);
    }
}
