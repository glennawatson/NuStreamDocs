// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Behavior tests for <c>PagefindOptionsExtensions</c>.</summary>
public class PagefindOptionsExtensionsTests
{
    /// <summary>Prefix for release-note pages.</summary>
    private const string ChangelogPrefix = "changelog/";

    /// <summary>Gets the section-priority fixture shared by the byte overloads.</summary>
    private static ReadOnlySpan<byte> ApiSectionPriority => "api/:-200"u8;

    /// <summary><c>WithSearchableFrontmatterKeys(string[])</c> replaces the existing list, encoding to UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSearchableFrontmatterKeysStringReplaces()
    {
        const int keyCount = 2;
        var updated = PagefindOptions.Default.WithSearchableFrontmatterKeys("tags", "summary");
        await Assert.That(updated.SearchableFrontmatterKeys.Length).IsEqualTo(keyCount);
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
        const int keyCount = 3;
        var seeded = PagefindOptions.Default.WithSearchableFrontmatterKeys("tags");
        var afterString = seeded.AddSearchableFrontmatterKeys("summary");
        var afterBytes = afterString.AddSearchableFrontmatterKeys([[.. "author"u8]]);
        await Assert.That(afterBytes.SearchableFrontmatterKeys.Length).IsEqualTo(keyCount);
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
        const int minimumLength = 4;
        var custom = PagefindOptions.Default with { MinTokenLength = minimumLength };
        var updated = custom.AddSearchableFrontmatterKeys("tags");
        await Assert.That(updated.MinTokenLength).IsEqualTo(minimumLength);
    }

    /// <summary><c>WithSectionPriorities(string)</c> encodes UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSectionPrioritiesString()
    {
        var updated = PagefindOptions.Default.WithSectionPriorities("guide/:80");
        await Assert.That(Encoding.UTF8.GetString(updated.SectionPriorities)).IsEqualTo("guide/:80");
    }

    /// <summary><c>WithSectionPriorities(byte[])</c> stores the supplied bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSectionPrioritiesBytesStoresVerbatim()
    {
        byte[] value = [.. ApiSectionPriority];
        var updated = PagefindOptions.Default.WithSectionPriorities(value);
        await Assert.That(updated.SectionPriorities).IsSameReferenceAs(value);
    }

    /// <summary><c>WithSectionPriorities(ReadOnlySpan&lt;byte&gt;)</c> accepts u8 literals directly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithSectionPrioritiesSpanAcceptsU8Literal()
    {
        var updated = PagefindOptions.Default.WithSectionPriorities(ApiSectionPriority);
        await Assert.That(updated.SectionPriorities.AsSpan().SequenceEqual(ApiSectionPriority)).IsTrue();
    }

    /// <summary><c>WithOutputSubdirectory</c> replaces the subdirectory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithOutputSubdirectoryReplaces()
    {
        var updated = PagefindOptions.Default.WithOutputSubdirectory("find");
        await Assert.That(updated.OutputSubdirectory.Value).IsEqualTo("find");
    }

    /// <summary><c>WithMinTokenLength</c> replaces the filter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithMinTokenLengthReplaces()
    {
        const int minimumLength = 7;
        var updated = PagefindOptions.Default.WithMinTokenLength(minimumLength);
        await Assert.That(updated.MinTokenLength).IsEqualTo(minimumLength);
    }

    /// <summary><c>WithRunCli</c> toggles the flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithRunCliToggles()
    {
        await Assert.That(PagefindOptions.Default.WithRunCli(false).RunCli).IsFalse();
        await Assert.That(PagefindOptions.Default.WithRunCli(false).WithRunCli(true).RunCli).IsTrue();
    }

    /// <summary><c>WithBinaryPath</c> replaces the override; <c>default</c> clears it.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithBinaryPathReplacesAndClears()
    {
        var set = PagefindOptions.Default.WithBinaryPath(new("/opt/pagefind/pagefind"));
        await Assert.That(set.BinaryPath.Value).IsEqualTo("/opt/pagefind/pagefind");
        await Assert.That(set.WithBinaryPath(default).BinaryPath.IsEmpty).IsTrue();
    }

    /// <summary><c>WithStrictBinaryRequired</c> toggles the flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithStrictBinaryRequiredToggles() => await Assert
        .That(PagefindOptions.Default.WithStrictBinaryRequired(true).StrictBinaryRequired).IsTrue();

    /// <summary><c>WithExcludePathPrefixes(string[])</c> replaces the list, encoding to UTF-8.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExcludePathPrefixesStringReplaces()
    {
        const int prefixCount = 2;
        var updated = PagefindOptions.Default.WithExcludePathPrefixes("api/", ChangelogPrefix);
        await Assert.That(updated.ExcludePathPrefixes.Length).IsEqualTo(prefixCount);
        await Assert.That(Encoding.UTF8.GetString(updated.ExcludePathPrefixes[0])).IsEqualTo("api/");
        await Assert.That(Encoding.UTF8.GetString(updated.ExcludePathPrefixes[1])).IsEqualTo(ChangelogPrefix);
    }

    /// <summary><c>WithExcludePathPrefixes(byte[][])</c> stores the supplied bytes verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithExcludePathPrefixesBytesStoresVerbatim()
    {
        byte[][] prefixes = [[.. "api/"u8]];
        var updated = PagefindOptions.Default.WithExcludePathPrefixes(prefixes);
        await Assert.That(updated.ExcludePathPrefixes).IsSameReferenceAs(prefixes);
    }

    /// <summary><c>AddExcludePathPrefixes</c> appends across the string, byte[][], and span overloads.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExcludePathPrefixesAppends()
    {
        const int prefixCount = 3;
        var afterString = PagefindOptions.Default.AddExcludePathPrefixes("api/");
        var afterBytes = afterString.AddExcludePathPrefixes([[.. "changelog/"u8]]);
        var afterSpan = afterBytes.AddExcludePathPrefixes("internal/"u8);
        await Assert.That(afterSpan.ExcludePathPrefixes.Length).IsEqualTo(prefixCount);
        await Assert.That(afterSpan.ExcludePathPrefixes[2].AsSpan().SequenceEqual("internal/"u8)).IsTrue();
    }

    /// <summary><c>AddExcludePathPrefixes</c> with an empty list is a no-op that returns the input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExcludePathPrefixesEmptyIsNoOp()
    {
        byte[][] emptyBytes = [];
        ApiCompatString[] emptyText = [];
        var seeded = PagefindOptions.Default.WithExcludePathPrefixes("api/");
        await Assert.That(seeded.AddExcludePathPrefixes(emptyBytes).ExcludePathPrefixes)
            .IsSameReferenceAs(seeded.ExcludePathPrefixes);
        await Assert.That(seeded.AddExcludePathPrefixes(emptyText).ExcludePathPrefixes)
            .IsSameReferenceAs(seeded.ExcludePathPrefixes);
    }

    /// <summary><c>ClearExcludePathPrefixes</c> empties the list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearExcludePathPrefixesEmpties()
    {
        var cleared = PagefindOptions.Default.WithExcludePathPrefixes("api/").ClearExcludePathPrefixes();
        await Assert.That(cleared.ExcludePathPrefixes.Length).IsEqualTo(0);
    }

    /// <summary>Unrelated knobs survive exclude-prefix edits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnrelatedKnobsPreservedAcrossExcludeEdits()
    {
        const int minimumLength = 5;
        var custom = PagefindOptions.Default with { MinTokenLength = minimumLength };
        var updated = custom.AddExcludePathPrefixes("api/");
        await Assert.That(updated.MinTokenLength).IsEqualTo(minimumLength);
    }
}
