// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc.Tests;

/// <summary>Tests for <c>HeadingSlugifier</c>.</summary>
public class HeadingSlugifierTests
{
    /// <summary>Basic ASCII slugification.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BasicAsciiSlug() => await Assert.That(HeadingSlugifier.Slugify("Hello World")).IsEqualTo("hello-world");

    /// <summary>Punctuation collapses and trims.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TrimsLeadingAndTrailingPunctuation() => await Assert.That(HeadingSlugifier.Slugify("  ~!Foo --- Bar?? ")).IsEqualTo("foo-bar");

    /// <summary>Empty / pure-punctuation input gets fallback.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyInputFallsBackToSection()
    {
        await Assert.That(HeadingSlugifier.Slugify("???")).IsEqualTo("section");
        await Assert.That(HeadingSlugifier.Slugify(string.Empty)).IsEqualTo("section");
    }

    /// <summary>Duplicates within a page get numeric suffixes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DuplicateSlugsGetSuffixes()
    {
        var html = "<h2>Intro</h2><h2>Intro</h2><h2>Intro</h2>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        var (slugged, collisions) = HeadingSlugifier.AssignSlugs(html, headings);
        await Assert.That(slugged.Length).IsEqualTo(3);
        await Assert.That(slugged[0].Slug).IsEqualTo("intro");
        await Assert.That(slugged[1].Slug).IsEqualTo("intro-2");
        await Assert.That(slugged[2].Slug).IsEqualTo("intro-3");
        await Assert.That(collisions).IsEqualTo(2);
    }

    /// <summary>Existing id is preserved as the base slug.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExistingIdIsRespected()
    {
        var html = "<h2 id=\"custom\">Heading</h2>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        var (slugged, _) = HeadingSlugifier.AssignSlugs(html, headings);
        await Assert.That(slugged[0].Slug).IsEqualTo("custom");
    }
}
