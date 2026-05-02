// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;

namespace NuStreamDocs.Feed.Tests;

/// <summary>Tests for FeedPlugin's lifecycle hooks and OnFinalizeAsync conditionals.</summary>
public class FeedPluginTests
{
    /// <summary>Two-arg constructor sets defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoArgCtor()
    {
        var plugin = new FeedPlugin(new("https://x.test/", "T", "D", "blog"), TimeProvider.System);
        await Assert.That(plugin.Name).IsEqualTo("feed");
    }

    /// <summary>OnConfigureAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnConfigureAsyncSucceeds() =>
        await new FeedPlugin(new("https://x.test/", "T", "D", "blog"))
            .OnConfigureAsync(new(default, "/in", "/out", []), CancellationToken.None);

    /// <summary>OnRenderPageAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderPageAsyncSucceeds()
    {
        var sink = new ArrayBufferWriter<byte>(8);
        await new FeedPlugin(new("https://x.test/", "T", "D", "blog"))
            .OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
    }

    /// <summary>OnFinalizeAsync runs against an empty output dir without error.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinalizeAsyncEmptyDirSucceeds()
    {
        using var dir = new TempDir();
        var plugin = new FeedPlugin(new("https://x.test/", "T", "D", "blog"));
        await plugin.OnConfigureAsync(new(default, dir.Root, dir.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(dir.Root), CancellationToken.None);
    }

    /// <summary>FeedFormats.None short-circuits OnFinalizeAsync without writing anything.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FormatsNoneShortCircuits()
    {
        using var dir = new TempDir();
        var opts = new FeedOptions("https://x.test/", "T", "D", "blog") { Formats = FeedFormats.None };
        var plugin = new FeedPlugin(opts);
        await plugin.OnConfigureAsync(new(default, dir.Root, dir.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(dir.Root), CancellationToken.None);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "feed.xml"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "atom.xml"))).IsFalse();
    }

    /// <summary>An empty input root short-circuits OnFinalizeAsync.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputRootShortCircuits()
    {
        using var dir = new TempDir();
        var plugin = new FeedPlugin(new("https://x.test/", "T", "D", "blog"));
        await plugin.OnFinalizeAsync(new(dir.Root), CancellationToken.None);
        await Assert.That(Directory.GetFiles(dir.Root, "*", SearchOption.AllDirectories)).IsEmpty();
    }

    /// <summary>An empty post directory yields no feed files.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyPostsDirNoOutput()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(Path.Combine(dir.Root, "blog"));
        var plugin = new FeedPlugin(new("https://x.test/", "T", "D", "blog"));
        await plugin.OnConfigureAsync(new(default, dir.Root, dir.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(dir.Root), CancellationToken.None);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "blog", "feed.xml"))).IsFalse();
    }

    /// <summary>A populated posts directory yields the configured feed files at the deterministic clock.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PopulatedPostsEmitsRssAndAtom()
    {
        using var dir = new TempDir();
        var posts = Path.Combine(dir.Root, "blog");
        Directory.CreateDirectory(posts);
        await File.WriteAllTextAsync(
            Path.Combine(posts, "2026-04-01-hello.md"),
            "---\ntitle: Hi\n---\nbody\n");

        var clock = new FakeTimeProvider(new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        var plugin = new FeedPlugin(new("https://x.test/", "T", "D", "blog"), clock);
        await plugin.OnConfigureAsync(new(default, dir.Root, dir.Root, []), CancellationToken.None);
        await plugin.OnFinalizeAsync(new(dir.Root), CancellationToken.None);

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
        var plugin = new FeedPlugin(new("https://x.test/", "T", "D", "blog"), TimeProvider.System, NullLogger.Instance);
        await Assert.That(plugin.Name).IsEqualTo("feed");
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
