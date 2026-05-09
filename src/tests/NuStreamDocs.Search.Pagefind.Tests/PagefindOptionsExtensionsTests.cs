// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Behavior tests for <c>PagefindOptionsExtensions</c>.</summary>
public class PagefindOptionsExtensionsTests
{
    /// <summary><c>WithSearchableFrontmatterKeys(string[])</c> replaces the existing list, encoding to UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSearchableFrontmatterKeysStringReplaces()
    {
        var updated = PagefindOptions.Default.WithSearchableFrontmatterKeys("tags", "summary");
        await Assert.That(updated.SearchableFrontmatterKeys.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(updated.SearchableFrontmatterKeys[0])).IsEqualTo("tags");
    }

    /// <summary><c>WithSearchableFrontmatterKeys(byte[][])</c> stores the supplied bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSearchableFrontmatterKeysBytesStoresVerbatim()
    {
        byte[][] keys = [[.. "tags"u8]];
        var updated = PagefindOptions.Default.WithSearchableFrontmatterKeys(keys);
        await Assert.That(updated.SearchableFrontmatterKeys).IsSameReferenceAs(keys);
    }

    /// <summary><c>AddSearchableFrontmatterKeys</c> appends.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddSearchableFrontmatterKeysAppends()
    {
        var seeded = PagefindOptions.Default.WithSearchableFrontmatterKeys("tags");
        var afterString = seeded.AddSearchableFrontmatterKeys("summary");
        var afterBytes = afterString.AddSearchableFrontmatterKeys([[.. "author"u8]]);
        await Assert.That(afterBytes.SearchableFrontmatterKeys.Length).IsEqualTo(3);
    }

    /// <summary><c>ClearSearchableFrontmatterKeys</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearSearchableFrontmatterKeysEmpties()
    {
        var cleared = PagefindOptions.Default
            .WithSearchableFrontmatterKeys("tags")
            .ClearSearchableFrontmatterKeys();
        await Assert.That(cleared.SearchableFrontmatterKeys.Length).IsEqualTo(0);
    }

    /// <summary>Single-byte-span frontmatter-key overload accepts u8 literals directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpanOverloadAcceptsU8Literal()
    {
        var updated = PagefindOptions.Default.AddSearchableFrontmatterKeys("tags"u8);
        await Assert.That(updated.SearchableFrontmatterKeys[0].AsSpan().SequenceEqual("tags"u8)).IsTrue();
    }

    /// <summary>Other knobs survive frontmatter-key edits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnrelatedKnobsPreserved()
    {
        var custom = PagefindOptions.Default with { MinTokenLength = 4 };
        var updated = custom.AddSearchableFrontmatterKeys("tags");
        await Assert.That(updated.MinTokenLength).IsEqualTo(4);
    }

    /// <summary><c>WithSectionPriorities(string)</c> encodes UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSectionPrioritiesString()
    {
        var updated = PagefindOptions.Default.WithSectionPriorities("guide/:80");
        await Assert.That(Encoding.UTF8.GetString(updated.SectionPriorities)).IsEqualTo("guide/:80");
    }

    /// <summary>Null arguments throw.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullArgumentsThrow()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => PagefindOptions.Default.WithSearchableFrontmatterKeys((byte[][])null!));
        var ex2 = Assert.Throws<ArgumentNullException>(static () => PagefindOptions.Default.AddSearchableFrontmatterKeys((ApiCompatString[])null!));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
    }
}
