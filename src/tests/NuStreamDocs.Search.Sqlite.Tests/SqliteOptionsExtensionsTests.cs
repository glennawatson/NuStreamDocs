// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Search.Sqlite.Tests;

/// <summary>Coverage for <c>SqliteOptionsExtensions</c>.</summary>
public class SqliteOptionsExtensionsTests
{
    /// <summary><c>WithExcludePathPrefixes</c> replaces the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExcludePathPrefixesReplaces()
    {
        var o = SqliteOptions.Default.WithExcludePathPrefixes("api/", "drafts/");
        await Assert.That(o.ExcludePathPrefixes.Length).IsEqualTo(2);
    }

    /// <summary><c>AddExcludePathPrefixes</c> appends.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExcludePathPrefixesAppends()
    {
        var o = SqliteOptions.Default.WithExcludePathPrefixes("api/").AddExcludePathPrefixes("drafts/");
        await Assert.That(o.ExcludePathPrefixes.Length).IsEqualTo(2);
    }

    /// <summary>The single-byte-span exclude overload accepts u8 literals directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExcludePathPrefixesSpanOverload()
    {
        var o = SqliteOptions.Default.AddExcludePathPrefixes("api/"u8);
        await Assert.That(o.ExcludePathPrefixes[0].AsSpan().SequenceEqual("api/"u8)).IsTrue();
    }

    /// <summary><c>ClearExcludePathPrefixes</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearExcludePathPrefixesEmpties()
    {
        var o = SqliteOptions.Default.WithExcludePathPrefixes("api/").ClearExcludePathPrefixes();
        await Assert.That(o.ExcludePathPrefixes.Length).IsEqualTo(0);
    }

    /// <summary><c>WithIndexFullBody</c> toggles the flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithIndexFullBodyToggles()
    {
        await Assert.That(SqliteOptions.Default.WithIndexFullBody(false).IndexFullBody).IsFalse();
        await Assert.That(SqliteOptions.Default.IndexFullBody).IsTrue();
    }

    /// <summary><c>WithMinTokenLength</c> and <c>WithOutputSubdirectory</c> round-trip.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScalarSettersRoundTrip()
    {
        var o = SqliteOptions.Default.WithMinTokenLength(5).WithOutputSubdirectory("find");
        await Assert.That(o.MinTokenLength).IsEqualTo(5);
        await Assert.That(o.OutputSubdirectory.Value).IsEqualTo("find");
    }

    /// <summary>Frontmatter-key replace + add behave like the other search backends.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FrontmatterKeyReplaceAndAdd()
    {
        var seeded = SqliteOptions.Default.WithSearchableFrontmatterKeys("tags");
        var added = seeded.AddSearchableFrontmatterKeys("summary", "author");
        await Assert.That(added.SearchableFrontmatterKeys.Length).IsEqualTo(3);
    }

    /// <summary><c>WithSectionPriorities(string)</c> encodes UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSectionPrioritiesString()
    {
        var o = SqliteOptions.Default.WithSectionPriorities("guide/:80");
        await Assert.That(Encoding.UTF8.GetString(o.SectionPriorities)).IsEqualTo("guide/:80");
    }
}
