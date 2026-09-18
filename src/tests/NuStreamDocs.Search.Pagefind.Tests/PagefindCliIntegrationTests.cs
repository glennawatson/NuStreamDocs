// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
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
    /// <summary>Pagefind executable and output directory name.</summary>
    private const string PagefindName = "pagefind";

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

        var pagefindDir = Path.Combine(dir.Root, PagefindName);
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
        await Assert.That(Directory.Exists(Path.Combine(dir.Root, PagefindName))).IsFalse();
    }

    /// <summary>Normal exit codes determine whether invocation succeeds.</summary>
    /// <param name="exitCode">Exit code returned by the child process.</param>
    /// <param name="expected">Expected success result.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, true)]
    [Arguments(23, false)]
    [Arguments(143, false)]
    public async Task RunAsync_NormalExit_ReturnsSuccess(int exitCode, bool expected, CancellationToken cancellationToken)
    {
        using var dir = new TempDir();
        var binary = await WriteExecutableAsync(dir.Root, $"exit {exitCode}", cancellationToken);
        var options = PagefindOptions.Default with { BinaryPath = binary };

        var result = await PagefindCli.RunAsync(dir.Root, options, NullLogger.Instance, cancellationToken);

        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>Strict failures preserve the ordinary exit code and captured stderr.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunAsync_StrictNormalExit_ReportsExitCode(CancellationToken cancellationToken)
    {
        using var dir = new TempDir();
        var binary = await WriteExecutableAsync(dir.Root, "echo indexing-failed >&2\nexit 143", cancellationToken);
        var options = PagefindOptions.Default with { BinaryPath = binary, StrictBinaryRequired = true };

        var exception = await Assert.That(() => PagefindCli.RunAsync(dir.Root, options, NullLogger.Instance, cancellationToken))
            .Throws<InvalidOperationException>();

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("Pagefind exited with code 143");
        await Assert.That(exception.Message).Contains("indexing-failed");
    }

    /// <summary>Signal termination fails the invocation and is identified separately on .NET 11.</summary>
    /// <param name="strict">Whether failure throws instead of returning false.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_SignalTermination_ReportsFailure(bool strict, CancellationToken cancellationToken)
    {
        using var dir = new TempDir();
        var binary = await WriteExecutableAsync(dir.Root, "echo interrupted >&2\nkill -TERM $$", cancellationToken);
        var options = PagefindOptions.Default with { BinaryPath = binary, StrictBinaryRequired = strict };

        if (!strict)
        {
            var result = await PagefindCli.RunAsync(dir.Root, options, NullLogger.Instance, cancellationToken);
            await Assert.That(result).IsFalse();
            return;
        }

        var exception = await Assert.That(() => PagefindCli.RunAsync(dir.Root, options, NullLogger.Instance, cancellationToken))
            .Throws<InvalidOperationException>();
        await Assert.That(exception).IsNotNull();
#if NET11_0_OR_GREATER
        await Assert.That(exception!.Message).Contains("Pagefind terminated by signal SIGTERM");
#else
        await Assert.That(exception!.Message).Contains("Pagefind exited with code 143");
#endif
        await Assert.That(exception.Message).Contains("interrupted");
    }

    /// <summary>Cancellation terminates a child that has closed its output pipes and propagates to the caller.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Timeout(10_000)]
    public async Task RunAsync_CanceledAfterOutputCloses_KillsChild(CancellationToken cancellationToken)
    {
        const int readinessTimeoutSeconds = 5;
        using var dir = new TempDir();
        var binary = await WriteExecutableAsync(
            dir.Root,
            "exec 1>&- 2>&-\necho $$ > process.pid\nmv process.pid ready\nexec sleep 60",
            cancellationToken);
        var options = PagefindOptions.Default with { BinaryPath = binary, StrictBinaryRequired = true };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var invocation = PagefindCli.RunAsync(dir.Root, options, NullLogger.Instance, cancellation.Token);
        var ready = Path.Combine(dir.Root, "ready");

        try
        {
            await Assert.That(() => File.Exists(ready)).WaitsFor(
                static assertion => assertion.IsTrue(),
                TimeSpan.FromSeconds(readinessTimeoutSeconds),
                cancellationToken: cancellationToken);
            var processId = int.Parse(await File.ReadAllTextAsync(ready, cancellationToken), CultureInfo.InvariantCulture);
            using var child = Process.GetProcessById(processId);
            await cancellation.CancelAsync();

            await Assert.That(async () => _ = await invocation).Throws<OperationCanceledException>();
            await child.WaitForExitAsync(cancellationToken);
            await Assert.That(child.HasExited).IsTrue();
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    /// <summary>Creates a Unix process fixture with the requested exit behavior.</summary>
    /// <param name="directory">Fixture directory.</param>
    /// <param name="body">Shell commands executed by the fixture.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>The executable fixture path.</returns>
    private static async Task<FilePath> WriteExecutableAsync(string directory, string body, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("The process fixture requires a POSIX shell.");
            return default;
        }

        var path = Path.Combine(directory, PagefindName);
        await File.WriteAllTextAsync(path, $"#!/bin/sh\n{body}\n", cancellationToken);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new(path);
    }

    /// <summary>Probe that mirrors the runner's resolution order — true when a binary will be found at run time.</summary>
    /// <returns>Whether <see cref="PagefindCli.RunAsync"/> would have a native to invoke.</returns>
    private static bool IsBinaryAvailable()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var fileName = OperatingSystem.IsWindows() ? "pagefind.exe" : PagefindName;
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
                $"smkd-pf-cli-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Root);
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
