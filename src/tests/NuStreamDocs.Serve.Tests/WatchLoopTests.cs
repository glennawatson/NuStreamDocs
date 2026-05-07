// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;

namespace NuStreamDocs.Serve.Tests;

/// <summary>Tests for <see cref="WatchLoop"/> debounce behavior.</summary>
public class WatchLoopTests
{
    /// <summary>Touching files in quick succession yields a single debounced batch.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BurstOfWritesCoalescesIntoOneBatch()
    {
        var dir = CreateTempDir();
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
            using WatchLoop watcher = new(dir, ignoreRoot: null, debounceMs: 200, ignoredSegments: null, NullLogger.Instance);
            var enumerator = watcher.WaitAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNext = enumerator.MoveNextAsync();
            await Task.Delay(50, cts.Token);

            await File.WriteAllTextAsync(Path.Combine(dir, "a.md"), "x", cts.Token);
            await File.WriteAllTextAsync(Path.Combine(dir, "b.md"), "x", cts.Token);
            await File.WriteAllTextAsync(Path.Combine(dir, "c.md"), "x", cts.Token);

            var advanced = await moveNext.AsTask().WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
            await Assert.That(advanced).IsTrue();
            await Assert.That(enumerator.Current.Count).IsGreaterThanOrEqualTo(1);
            await enumerator.DisposeAsync();
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    /// <summary>Events under the ignored output root never reach the channel.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EventsUnderIgnoreRootAreSuppressed()
    {
        var dir = CreateTempDir();
        var ignored = Path.Combine(dir, "site");
        Directory.CreateDirectory(ignored);
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(800));
            using WatchLoop watcher = new(dir, ignored, debounceMs: 200, ignoredSegments: null, NullLogger.Instance);
            var enumerator = watcher.WaitAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNext = enumerator.MoveNextAsync();
            await Task.Delay(50, CancellationToken.None);

            await File.WriteAllTextAsync(Path.Combine(ignored, "page.html"), "x", CancellationToken.None);

            var advanced = await moveNext.AsTask();
            await Assert.That(advanced).IsFalse();
            await enumerator.DisposeAsync();
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    /// <summary>Events whose path contains a denylisted segment never reach the channel.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EventsUnderIgnoredSegmentAreSuppressed()
    {
        var dir = CreateTempDir();
        var binDir = Path.Combine(dir, "bin");
        Directory.CreateDirectory(binDir);
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(800));
            using WatchLoop watcher = new(dir, ignoreRoot: null, debounceMs: 200, ignoredSegments: ["bin"], NullLogger.Instance);
            var enumerator = watcher.WaitAsync(cts.Token).GetAsyncEnumerator(cts.Token);
            var moveNext = enumerator.MoveNextAsync();
            await Task.Delay(50, CancellationToken.None);

            await File.WriteAllTextAsync(Path.Combine(binDir, "noisy.dll"), "x", CancellationToken.None);

            var advanced = await moveNext.AsTask();
            await Assert.That(advanced).IsFalse();
            await enumerator.DisposeAsync();
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    /// <summary>Helper that creates a unique temp directory.</summary>
    /// <returns>Absolute path of the new directory.</returns>
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NuStreamDocs.Serve.Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Best-effort cleanup; the watcher may still hold handles briefly.</summary>
    /// <param name="dir">Directory to remove.</param>
    private static void CleanupTempDir(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // tests run in sequence; the next run uses a fresh GUID name.
        }
        catch (UnauthorizedAccessException)
        {
            // same.
        }
    }
}
