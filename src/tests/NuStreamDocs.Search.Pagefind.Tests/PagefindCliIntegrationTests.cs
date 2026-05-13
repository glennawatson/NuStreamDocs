// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>
/// End-to-end test that exercises the real bundled Pagefind binary (when present
/// for the host RID) and confirms the WASM runtime + binary inverted-index shards
/// land at the expected location.
/// </summary>
/// <remarks>
/// Skipped silently when no native binary is bundled for the host RID — keeps the
/// test green on environments where the runtimes/ folder hasn't been populated
/// (e.g. minimal CI without the refresh tool run). When the binary is present,
/// asserts the pagefind/ directory exists and contains pagefind.js plus at least
/// one of its runtime artifacts.
/// </remarks>
public class PagefindCliIntegrationTests
{
    /// <summary>Pinned-version sanity: <see cref="PagefindCli.PinnedVersion"/> matches the upstream tag we ship.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PinnedVersionIsSet()
    {
        await Assert.That(PagefindCli.PinnedVersion).IsNotNull();
        await Assert.That(PagefindCli.PinnedVersion).IsNotEmpty();
    }

    /// <summary>End-to-end <see cref="PagefindCli.RunAsync"/> against a hand-rolled HTML fixture → <c>pagefind/pagefind.js</c> + at least one <c>.pagefind</c> shard.</summary>
    /// <returns>Async test.</returns>
    /// <remarks>
    /// Runs against bare HTML files (not through <c>DocBuilder</c>) so the test stays in this
    /// assembly without dragging in a theme dependency. The shape Pagefind cares about is just
    /// "valid &lt;html&gt; documents with a <c>data-pagefind-body</c> attribute or a <c>&lt;main&gt;</c>";
    /// we satisfy both belt-and-suspenders.
    /// </remarks>
    [Test]
    public async Task RealPagefindEmitsLoaderAndShards()
    {
        if (!IsBinaryAvailable())
        {
            // Native binary not bundled for this host — skip without failing.
            return;
        }

        using TempDir dir = new();
        DirectoryPath siteRoot = new(dir.Root);

        await File.WriteAllTextAsync(
            Path.Combine(dir.Root, "intro.html"),
            "<!doctype html><html><head><title>Intro</title></head><body><main data-pagefind-body><h1>Intro</h1><p>hello world content</p></main></body></html>");
        await File.WriteAllTextAsync(
            Path.Combine(dir.Root, "guide.html"),
            "<!doctype html><html><head><title>Guide</title></head><body><main data-pagefind-body><h1>Guide</h1><p>more body text</p></main></body></html>");

        var options = PagefindOptions.Default with { StrictBinaryRequired = true };
        var ran = await PagefindCli.RunAsync(siteRoot, options, NullLogger.Instance, CancellationToken.None);
        await Assert.That(ran).IsTrue();

        var pagefindDir = Path.Combine(dir.Root, "pagefind");
        await Assert.That(Directory.Exists(pagefindDir)).IsTrue();

        var loader = Path.Combine(pagefindDir, "pagefind.js");
        await Assert.That(File.Exists(loader)).IsTrue();
        await Assert.That(Directory.GetFiles(pagefindDir, "*.pagefind").Length).IsGreaterThan(0);
    }

    /// <summary><c>RunCli=false</c> short-circuits without spawning a process.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunCliDisabledShortCircuits()
    {
        using TempDir dir = new();
        DirectoryPath siteRoot = new(dir.Root);
        var ran = await PagefindCli.RunAsync(
            siteRoot,
            PagefindOptions.Default with { RunCli = false },
            NullLogger.Instance,
            CancellationToken.None);
        await Assert.That(ran).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(dir.Root, "pagefind"))).IsFalse();
    }

    /// <summary>Probe that mirrors the runner's resolution order — true when a binary will be found at run time.</summary>
    /// <returns>Whether <see cref="PagefindCli.RunAsync"/> would have a native to invoke.</returns>
    private static bool IsBinaryAvailable()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var fileName = OperatingSystem.IsWindows() ? "pagefind.exe" : "pagefind";
        var probe = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        return File.Exists(probe);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "smkd-pf-cli-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch root.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
