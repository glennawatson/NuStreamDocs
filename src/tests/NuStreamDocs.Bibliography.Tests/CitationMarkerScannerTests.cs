// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Pandoc-style citation marker grammar — single, multi, locator-bearing, escape-into-code.</summary>
public class CitationMarkerScannerTests
{
    /// <summary>A bare <c>[@key]</c> marker is captured with the right span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleKeyIsRecognised()
    {
        var markers = CitationMarkerScanner.Find("see [@mabo] for context"u8);
        await Assert.That(markers).HasSingleItem();
        await Assert.That(markers[0].Cites).HasSingleItem();
        await Assert.That(markers[0].Cites[0].Key).IsEqualTo("mabo");
        await Assert.That(markers[0].Cites[0].Locator.Kind).IsEqualTo(LocatorKind.None);
    }

    /// <summary>A locator label and value are split correctly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LocatorWithLabelIsClassified()
    {
        var markers = CitationMarkerScanner.Find("[@mabo, p 23]"u8);
        var loc = markers[0].Cites[0].Locator;
        await Assert.That(loc.Kind).IsEqualTo(LocatorKind.Page);
        await Assert.That(loc.Value).IsEqualTo("23");
    }

    /// <summary>Multi-cite <c>[@a; @b]</c> emits two refs in order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultiCiteSplitsOnSemicolon()
    {
        var markers = CitationMarkerScanner.Find("[@one; @two]"u8);
        await Assert.That(markers).HasSingleItem();
        await Assert.That(markers[0].Cites.Length).IsEqualTo(2);
        await Assert.That(markers[0].Cites[0].Key).IsEqualTo("one");
        await Assert.That(markers[0].Cites[1].Key).IsEqualTo("two");
    }

    /// <summary>Markers inside fenced code are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeIsSkipped()
    {
        var markers = CitationMarkerScanner.Find("```\n[@nope]\n```\n[@yes]\n"u8);
        await Assert.That(markers).HasSingleItem();
        await Assert.That(markers[0].Cites[0].Key).IsEqualTo("yes");
    }

    /// <summary>Markers inside inline code spans are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodeIsSkipped()
    {
        var markers = CitationMarkerScanner.Find("`[@inline]` and [@real]"u8);
        await Assert.That(markers).HasSingleItem();
        await Assert.That(markers[0].Cites[0].Key).IsEqualTo("real");
    }

    /// <summary>Locator value with a hyphen range is preserved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LocatorRangeIsPreserved()
    {
        var markers = CitationMarkerScanner.Find("[@case, pp 23-25]"u8);
        await Assert.That(markers[0].Cites[0].Locator.Value).IsEqualTo("23-25");
    }

    /// <summary>An unrecognised label round-trips through <see cref="LocatorKind.Other"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownLabelIsOther()
    {
        var markers = CitationMarkerScanner.Find("[@x, foo 9]"u8);
        var loc = markers[0].Cites[0].Locator;
        await Assert.That(loc.Kind).IsEqualTo(LocatorKind.Other);
        await Assert.That(loc.Value).IsEqualTo("foo 9");
    }

    /// <summary>An empty source produces no markers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptySourceProducesNothing()
    {
        var markers = CitationMarkerScanner.Find(default);
        await Assert.That(markers).IsEmpty();
    }
}
