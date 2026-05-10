// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Search.Lunr.Tests;

/// <summary>Behavior tests for <c>LunrOptionsExtensions</c>.</summary>
public class LunrOptionsExtensionsTests
{
    /// <summary><c>WithLanguage</c> encodes the supplied string into UTF-8 bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithLanguageString()
    {
        var fr = LunrOptions.Default.WithLanguage("fr");
        await Assert.That(fr.Language.AsSpan().SequenceEqual("fr"u8)).IsTrue();
    }

    /// <summary>Span overload accepts a UTF-8 literal directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithLanguageSpan()
    {
        var de = LunrOptions.Default.WithLanguage("de"u8);
        await Assert.That(de.Language.AsSpan().SequenceEqual("de"u8)).IsTrue();
    }

    /// <summary>Byte-array overload stores the supplied array verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithLanguageBytesStoresVerbatim()
    {
        byte[] lang = [.. "es"u8];
        var es = LunrOptions.Default.WithLanguage(lang);
        await Assert.That(es.Language).IsSameReferenceAs(lang);
    }

    /// <summary><c>WithExtraStopwords</c> replaces the existing list, encoding to UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExtraStopwordsString()
    {
        var updated = LunrOptions.Default.WithExtraStopwords("foo", "bar");
        await Assert.That(updated.ExtraStopwords.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(updated.ExtraStopwords[0])).IsEqualTo("foo");
    }

    /// <summary><c>AddExtraStopwords</c> appends.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraStopwordsAppends()
    {
        var seeded = LunrOptions.Default.WithExtraStopwords("foo");
        var afterString = seeded.AddExtraStopwords("bar");
        var afterBytes = afterString.AddExtraStopwords([[.. "baz"u8]]);
        await Assert.That(afterBytes.ExtraStopwords.Length).IsEqualTo(3);
    }

    /// <summary><c>ClearExtraStopwords</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearExtraStopwordsEmpties()
    {
        var cleared = LunrOptions.Default
            .WithExtraStopwords("foo")
            .ClearExtraStopwords();
        await Assert.That(cleared.ExtraStopwords.Length).IsEqualTo(0);
    }

    /// <summary>The single-byte-span stopword overload accepts u8 literals directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadAcceptsU8Literal()
    {
        var updated = LunrOptions.Default.AddExtraStopwords("foo"u8);
        await Assert.That(updated.ExtraStopwords[0].AsSpan().SequenceEqual("foo"u8)).IsTrue();
    }

    /// <summary>Frontmatter-key replace + add work the same as Pagefind's variant.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FrontmatterKeyReplaceAndAdd()
    {
        var seeded = LunrOptions.Default.WithSearchableFrontmatterKeys("tags");
        var added = seeded.AddSearchableFrontmatterKeys("summary", "author");
        await Assert.That(added.SearchableFrontmatterKeys.Length).IsEqualTo(3);
    }

    /// <summary><c>WithSectionPriorities(string)</c> encodes UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSectionPrioritiesString()
    {
        var updated = LunrOptions.Default.WithSectionPriorities("guide/:80");
        await Assert.That(Encoding.UTF8.GetString(updated.SectionPriorities)).IsEqualTo("guide/:80");
    }
}
