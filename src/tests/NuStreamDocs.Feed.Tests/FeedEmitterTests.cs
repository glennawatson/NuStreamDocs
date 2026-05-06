// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Feed.Tests;

/// <summary>Direct unit tests for the static <c>FeedEmitter</c> helper extracted out of the plugin.</summary>
public class FeedEmitterTests
{
    /// <summary>Gets the deterministic test timestamp.</summary>
    private static DateTimeOffset TestTime => new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>WriteEnabledFormats with FeedFormats.None writes nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoneWritesNothing()
    {
        using FeedTempDir temp = new();
        var options = TestOptions(FeedFormats.None);
        var written = FeedEmitter.WriteEnabledFormats(options, temp.Root, [TestPost()], TestTime, NullLogger.Instance);
        await Assert.That(written).IsEqualTo(FeedFormats.None);
        await Assert.That(Directory.GetFiles(temp.Root)).IsEmpty();
    }

    /// <summary>Rss-only flag writes feed.xml.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RssOnlyWritesFeedXml()
    {
        using FeedTempDir temp = new();
        var written = FeedEmitter.WriteEnabledFormats(TestOptions(FeedFormats.Rss), temp.Root, [TestPost()], TestTime, NullLogger.Instance);
        await Assert.That(written).IsEqualTo(FeedFormats.Rss);
        await Assert.That(File.Exists(Path.Combine(temp.Root, "feed.xml"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(temp.Root, "atom.xml"))).IsFalse();
    }

    /// <summary>Atom-only flag writes atom.xml.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AtomOnlyWritesAtomXml()
    {
        using FeedTempDir temp = new();
        var written = FeedEmitter.WriteEnabledFormats(TestOptions(FeedFormats.Atom), temp.Root, [TestPost()], TestTime, NullLogger.Instance);
        await Assert.That(written).IsEqualTo(FeedFormats.Atom);
        await Assert.That(File.Exists(Path.Combine(temp.Root, "atom.xml"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(temp.Root, "feed.xml"))).IsFalse();
    }

    /// <summary>Both flags writes both files.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BothWritesBoth()
    {
        using FeedTempDir temp = new();
        var written = FeedEmitter.WriteEnabledFormats(TestOptions(FeedFormats.Both), temp.Root, [TestPost()], TestTime, NullLogger.Instance);
        await Assert.That(written).IsEqualTo(FeedFormats.Both);
        await Assert.That(File.Exists(Path.Combine(temp.Root, "feed.xml"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(temp.Root, "atom.xml"))).IsTrue();
    }

    /// <summary>Null guards fire on each parameter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullGuards()
    {
        var opts = TestOptions(FeedFormats.Both);
        Assert.Throws<ArgumentNullException>(() => FeedEmitter.WriteEnabledFormats(null!, "/tmp", [], TestTime, NullLogger.Instance));
        Assert.Throws<ArgumentException>(() => FeedEmitter.WriteEnabledFormats(opts, string.Empty, [], TestTime, NullLogger.Instance));
        Assert.Throws<ArgumentNullException>(() => FeedEmitter.WriteEnabledFormats(opts, "/tmp", null!, TestTime, NullLogger.Instance));
        var ex = Assert.Throws<ArgumentNullException>(() => FeedEmitter.WriteEnabledFormats(opts, "/tmp", [], TestTime, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Builds a sample post that satisfies FeedWriter's required fields.</summary>
    /// <returns>One sample post.</returns>
    private static BlogPost TestPost() =>
        new("posts/2026-01-01-hello.md", [.. "posts/2026-01-01-hello.html"u8], [.. "hello"u8], [.. "Hello"u8], [.. "Author"u8], new(2026, 1, 1), [], [.. "An excerpt."u8]);

    /// <summary>Builds a valid <see cref="FeedOptions"/> with the requested format flag.</summary>
    /// <param name="formats">Format flags.</param>
    /// <returns>Options.</returns>
    private static FeedOptions TestOptions(FeedFormats formats) =>
        new("https://x.test/", "Site", "Description", "posts", "feeds", formats, MaxItems: 0);

    /// <summary>Disposable scratch directory.</summary>
    private sealed class FeedTempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="FeedTempDir"/> class.</summary>
        public FeedTempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-feed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch directory.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
