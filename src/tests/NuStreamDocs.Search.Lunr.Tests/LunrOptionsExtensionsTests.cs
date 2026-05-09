// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Lunr.Tests;

/// <summary>Behavior tests for <c>LunrOptionsExtensions</c>.</summary>
public class LunrOptionsExtensionsTests
{
    /// <summary><c>WithLanguage</c> swaps the language code; null collapses to empty string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithLanguage()
    {
        var fr = LunrOptions.Default.WithLanguage("fr");
        await Assert.That(fr.Language).IsEqualTo("fr");
        var nullToEmpty = LunrOptions.Default.WithLanguage(null!);
        await Assert.That(nullToEmpty.Language).IsEqualTo(string.Empty);
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

    /// <summary>Null arguments throw.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullArgumentsThrow()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => LunrOptions.Default.WithExtraStopwords((byte[][])null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => LunrOptions.Default.AddExtraStopwords((ApiCompatString[])null!));
        var ex3 = Assert.Throws<ArgumentNullException>(static () => LunrOptions.Default.WithSearchableFrontmatterKeys((byte[][])null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
        await Assert.That(ex3).IsNotNull();
    }
}
