// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Search.Tests;

/// <summary>Behavior tests for <c>SearchOptionsExtensions</c>'s frontmatter-key and stopword helpers.</summary>
public class SearchOptionsExtensionsTests
{
    /// <summary><c>WithSearchableFrontmatterKeys(string[])</c> replaces the existing list, encoding to UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSearchableFrontmatterKeysStringReplaces()
    {
        var updated = SearchOptions.Default.WithSearchableFrontmatterKeys("tags", "summary");
        await Assert.That(updated.SearchableFrontmatterKeys.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(updated.SearchableFrontmatterKeys[0])).IsEqualTo("tags");
        await Assert.That(Encoding.UTF8.GetString(updated.SearchableFrontmatterKeys[1])).IsEqualTo("summary");
    }

    /// <summary><c>WithSearchableFrontmatterKeys(byte[][])</c> stores the supplied bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSearchableFrontmatterKeysBytesStoresVerbatim()
    {
        byte[][] keys = [[.. "tags"u8], [.. "summary"u8]];
        var updated = SearchOptions.Default.WithSearchableFrontmatterKeys(keys);
        await Assert.That(updated.SearchableFrontmatterKeys).IsSameReferenceAs(keys);
    }

    /// <summary><c>AddSearchableFrontmatterKeys</c> appends to the existing list (string + byte forms).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddSearchableFrontmatterKeysAppends()
    {
        var seeded = SearchOptions.Default.WithSearchableFrontmatterKeys("tags");
        var afterString = seeded.AddSearchableFrontmatterKeys("summary");
        var afterBytes = afterString.AddSearchableFrontmatterKeys([[.. "author"u8]]);
        await Assert.That(afterBytes.SearchableFrontmatterKeys.Length).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(afterBytes.SearchableFrontmatterKeys[2])).IsEqualTo("author");
    }

    /// <summary><c>ClearSearchableFrontmatterKeys</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearSearchableFrontmatterKeysEmpties()
    {
        var cleared = SearchOptions.Default
            .WithSearchableFrontmatterKeys("tags")
            .ClearSearchableFrontmatterKeys();
        await Assert.That(cleared.SearchableFrontmatterKeys.Length).IsEqualTo(0);
    }

    /// <summary><c>WithExtraStopwords(string[])</c> replaces the existing list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExtraStopwordsStringReplaces()
    {
        var updated = SearchOptions.Default.WithExtraStopwords("foo", "bar");
        await Assert.That(updated.ExtraStopwords.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(updated.ExtraStopwords[0])).IsEqualTo("foo");
    }

    /// <summary><c>WithExtraStopwords(byte[][])</c> stores the supplied bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExtraStopwordsBytesStoresVerbatim()
    {
        byte[][] stopwords = [[.. "foo"u8]];
        var updated = SearchOptions.Default.WithExtraStopwords(stopwords);
        await Assert.That(updated.ExtraStopwords).IsSameReferenceAs(stopwords);
    }

    /// <summary><c>AddExtraStopwords</c> appends (string + byte forms).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraStopwordsAppends()
    {
        var seeded = SearchOptions.Default.WithExtraStopwords("foo");
        var afterString = seeded.AddExtraStopwords("bar");
        var afterBytes = afterString.AddExtraStopwords([[.. "baz"u8]]);
        await Assert.That(afterBytes.ExtraStopwords.Length).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(afterBytes.ExtraStopwords[2])).IsEqualTo("baz");
    }

    /// <summary><c>ClearExtraStopwords</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearExtraStopwordsEmpties()
    {
        var cleared = SearchOptions.Default
            .WithExtraStopwords("foo")
            .ClearExtraStopwords();
        await Assert.That(cleared.ExtraStopwords.Length).IsEqualTo(0);
    }

    /// <summary><c>AddXxx</c> with an empty input returns the source unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddWithEmptyInputIsNoOp()
    {
        var seeded = SearchOptions.Default.WithExtraStopwords("foo");
        var noOp = seeded.AddExtraStopwords(Array.Empty<string>());
        await Assert.That(noOp.ExtraStopwords).IsSameReferenceAs(seeded.ExtraStopwords);
    }

    /// <summary>Other fields are preserved across edits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OtherFieldsPreservedAcrossEdits()
    {
        var custom = SearchOptions.Default with { MinTokenLength = 4 };
        var updated = custom.AddSearchableFrontmatterKeys("tags").AddExtraStopwords("foo").ClearExtraStopwords();
        await Assert.That(updated.MinTokenLength).IsEqualTo(4);
        await Assert.That(updated.SearchableFrontmatterKeys.Length).IsEqualTo(1);
    }

    /// <summary>The single-entry <see cref="ReadOnlySpan{T}"/> overloads on the list adders accept <c>"..."u8</c> literals directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadsAcceptU8LiteralsDirectly()
    {
        var updated = SearchOptions.Default
            .AddSearchableFrontmatterKeys("tags"u8)
            .AddExtraStopwords("foo"u8);
        await Assert.That(updated.SearchableFrontmatterKeys.Length).IsEqualTo(1);
        await Assert.That(updated.SearchableFrontmatterKeys[0].AsSpan().SequenceEqual("tags"u8)).IsTrue();
        await Assert.That(updated.ExtraStopwords[0].AsSpan().SequenceEqual("foo"u8)).IsTrue();
    }

    /// <summary>Null arguments throw.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullArgumentsThrow()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => SearchOptions.Default.WithSearchableFrontmatterKeys((byte[][])null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => SearchOptions.Default.AddSearchableFrontmatterKeys((string[])null!));
        var ex3 = Assert.Throws<ArgumentNullException>(static () => SearchOptions.Default.WithExtraStopwords((byte[][])null!));
        var ex4 = Assert.Throws<ArgumentNullException>(static () => SearchOptions.Default.AddExtraStopwords((byte[][])null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
        await Assert.That(ex3).IsNotNull();
        await Assert.That(ex4).IsNotNull();
    }
}
