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
    public async Task BasicAsciiSlug() =>
        await Assert.That(HeadingSlugifier.SlugifyToBytes("Hello World"u8).AsSpan().SequenceEqual("hello-world"u8)).IsTrue();

    /// <summary>Punctuation collapses and trims.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TrimsLeadingAndTrailingPunctuation() =>
        await Assert.That(HeadingSlugifier.SlugifyToBytes("  ~!Foo --- Bar?? "u8).AsSpan().SequenceEqual("foo-bar"u8)).IsTrue();

    /// <summary>Empty / pure-punctuation input gets fallback.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyInputFallsBackToSection()
    {
        await Assert.That(HeadingSlugifier.SlugifyToBytes("???"u8).AsSpan().SequenceEqual("section"u8)).IsTrue();
        await Assert.That(HeadingSlugifier.SlugifyToBytes(default).AsSpan().SequenceEqual("section"u8)).IsTrue();
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
        await Assert.That(slugged[0].Slug.AsSpan().SequenceEqual("intro"u8)).IsTrue();
        await Assert.That(slugged[1].Slug.AsSpan().SequenceEqual("intro-2"u8)).IsTrue();
        await Assert.That(slugged[2].Slug.AsSpan().SequenceEqual("intro-3"u8)).IsTrue();
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
        await Assert.That(slugged[0].Slug.AsSpan().SequenceEqual("custom"u8)).IsTrue();
    }

    /// <summary>SlugifyToBytes handles ASCII direct without round-tripping through string.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SlugifyToBytesAscii() =>
        await Assert.That(HeadingSlugifier.SlugifyToBytes("Hello World"u8).AsSpan().SequenceEqual("hello-world"u8)).IsTrue();

    /// <summary>SlugifyToBytes returns the fallback when input strips to nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SlugifyToBytesFallback() =>
        await Assert.That(HeadingSlugifier.SlugifyToBytes("???"u8).AsSpan().SequenceEqual("section"u8)).IsTrue();
}
