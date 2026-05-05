// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;

namespace NuStreamDocs.Feed.Tests;

/// <summary>Tests for FeedPlugin's lifecycle hooks and FinalizeAsync conditionals.</summary>
public class FeedPluginTests
{
    /// <summary>Two-arg constructor sets defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoArgCtor()
    {
        FeedPlugin plugin = new(new("https://x.test/", "T", "D", "blog"), TimeProvider.System);
        await Assert.That(plugin.Name.SequenceEqual("feed"u8)).IsTrue();
    }

    /// <summary>ConfigureAsync runs successfully.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureAsyncSucceeds() =>
        await new FeedPlugin(new("https://x.test/", "T", "D", "blog"))
            .ConfigureAsync(new("/in", "/out", [], new()), CancellationToken.None);

    /// <summary>FinalizeAsync runs against an empty output dir without error.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FinalizeAsyncEmptyDirSucceeds()
    {
        using TempDir dir = new();
        FeedPlugin plugin = new(new("https://x.test/", "T", "D", "blog"));
        await plugin.ConfigureAsync(new(dir.Root, dir.Root, [], new()), CancellationToken.None);
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);
    }

    /// <summary>FeedFormats.None short-circuits FinalizeAsync without writing anything.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FormatsNoneShortCircuits()
    {
        using TempDir dir = new();
        FeedOptions opts = new("https://x.test/", "T", "D", "blog") { Formats = FeedFormats.None };
        FeedPlugin plugin = new(opts);
        await plugin.ConfigureAsync(new(dir.Root, dir.Root, [], new()), CancellationToken.None);
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "feed.xml"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "atom.xml"))).IsFalse();
    }

    /// <summary>An empty input root short-circuits FinalizeAsync.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputRootShortCircuits()
    {
        using TempDir dir = new();
        FeedPlugin plugin = new(new("https://x.test/", "T", "D", "blog"));
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);
        await Assert.That(Directory.GetFiles(dir.Root, "*", SearchOption.AllDirectories)).IsEmpty();
    }

    /// <summary>An empty post directory yields no feed files.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyPostsDirNoOutput()
    {
        using TempDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Root, "blog"));
        FeedPlugin plugin = new(new("https://x.test/", "T", "D", "blog"));
        await plugin.ConfigureAsync(new(dir.Root, dir.Root, [], new()), CancellationToken.None);
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "feed.xml"))).IsFalse();
    }

    /// <summary>A populated posts directory yields the configured feed files at the deterministic clock.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PopulatedPostsEmitsRssAndAtom()
    {
        using TempDir dir = new();
        var posts = Path.Combine(dir.Root, "blog");
        Directory.CreateDirectory(posts);
        await File.WriteAllTextAsync(
            Path.Combine(posts, "2026-04-01-hello.md"),
            "---\ntitle: Hi\n---\nbody\n");

        FakeTimeProvider clock = new(new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        FeedPlugin plugin = new(new("https://x.test/", "T", "D", "blog"), clock);
        await plugin.ConfigureAsync(new(dir.Root, dir.Root, [], new()), CancellationToken.None);
        await plugin.FinalizeAsync(new(dir.Root, []), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "feed.xml"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "atom.xml"))).IsTrue();
    }

    /// <summary>Null TimeProvider rejected by the explicit-clock ctor.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullTimeProviderThrows() =>
        await Assert.That(() => new FeedPlugin(new("https://x.test/", "T", "D", "blog"), null!))
            .Throws<ArgumentNullException>();

    /// <summary>Null logger rejected by the three-arg ctor.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullLoggerThrows() =>
        await Assert.That(() => new FeedPlugin(new("https://x.test/", "T", "D", "blog"), TimeProvider.System, null!))
            .Throws<ArgumentNullException>();

    /// <summary>Three-arg ctor accepts an explicit logger.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThreeArgCtorAcceptsLogger()
    {
        FeedPlugin plugin = new(new("https://x.test/", "T", "D", "blog"), TimeProvider.System, NullLogger.Instance);
        await Assert.That(plugin.Name.SequenceEqual("feed"u8)).IsTrue();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-feed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch root.</summary>
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

    /// <summary>Deterministic clock for the feed timestamp.</summary>
    /// <param name="utcNow">Initial wall-clock value.</param>
    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
