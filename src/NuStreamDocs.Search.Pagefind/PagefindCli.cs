// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Search.Pagefind.Logging;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>
/// Locates and invokes the bundled <c>pagefind</c> CLI binary against a
/// rendered site directory to produce the genuine Pagefind WASM runtime +
/// binary inverted-index shards under <c>&lt;site&gt;/pagefind/</c>.
/// </summary>
/// <remarks>
/// <para>
/// Per-RID binaries ship as content files under
/// <c>runtimes/&lt;rid&gt;/native/pagefind[.exe]</c>; the SDK copies the
/// matching one to the consumer's <c>bin/</c> on build. Resolution order at
/// run time:
/// </para>
/// <list type="number">
/// <item>An explicit <see cref="PagefindOptions.BinaryPath"/> override.</item>
/// <item><c>runtimes/&lt;rid&gt;/native/pagefind[.exe]</c> next to the host assembly.</item>
/// <item>A bare <c>pagefind</c> on PATH (developer fallback when natives haven't been pulled in).</item>
/// </list>
/// <para>
/// When <see cref="PagefindOptions.RunCli"/> is true and no binary is found, the runner logs
/// a warning and returns without throwing — the build still completes, but with no
/// <c>pagefind/</c> directory, so search degrades to "unavailable" in the browser. Set
/// <see cref="PagefindOptions.StrictBinaryRequired"/> to flip that to a hard failure (CI
/// publishes that must produce real shards).
/// </para>
/// </remarks>
public static class PagefindCli
{
    /// <summary>Gets the Pagefind release this package targets.</summary>
    /// <remarks>Matches the version of the binaries shipped in <c>runtimes/&lt;rid&gt;/native/</c>; bump in lockstep.</remarks>
    public static string PinnedVersion => "1.5.2";

    /// <summary>Discovers + invokes Pagefind against <paramref name="siteRoot"/>.</summary>
    /// <param name="siteRoot">Absolute path to the rendered site directory containing the HTML pages.</param>
    /// <param name="options">Plugin options — supplies the binary override, the output subdirectory, and the strict/run toggles.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when Pagefind ran and exited with code 0; false when the runner skipped (binary missing, <see cref="PagefindOptions.RunCli"/> off).</returns>
    public static async Task<bool> RunAsync(DirectoryPath siteRoot, PagefindOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(siteRoot.Value);
        ArgumentNullException.ThrowIfNull(logger);

        if (!options.RunCli)
        {
            return false;
        }

        var binary = ResolveBinaryPath(options.BinaryPath);
        if (binary is null)
        {
            return HandleMissingBinary(options.StrictBinaryRequired, logger);
        }

        return await InvokeAsync(binary, siteRoot, options, logger, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>"Binary missing" branch — throws when strict, else logs a warning and returns false.</summary>
    /// <param name="strict">When true, throws.</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <returns>Always false (only return path that doesn't throw).</returns>
    private static bool HandleMissingBinary(bool strict, ILogger logger)
    {
        if (strict)
        {
            throw new FileNotFoundException(
                "Pagefind CLI binary not found. Either ship the per-RID native via the NuStreamDocs.Search.Pagefind package, " +
                "set PagefindOptions.BinaryPath explicitly, or disable PagefindOptions.RunCli.");
        }

        PagefindCliLogging.LogBinaryMissing(logger, RuntimeInformation.RuntimeIdentifier);
        return false;
    }

    /// <summary>Spawns Pagefind, captures its stdio, waits, and propagates the result.</summary>
    /// <param name="binary">Resolved binary path.</param>
    /// <param name="siteRoot">Rendered site directory.</param>
    /// <param name="options">Plugin options (carries the strict toggle).</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True on exit-code-zero; false on tolerated failure (strict off).</returns>
    private static async Task<bool> InvokeAsync(string binary, DirectoryPath siteRoot, PagefindOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        ProcessStartInfo psi = new()
        {
            FileName = binary,
            WorkingDirectory = siteRoot.Value,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add("--site");
        psi.ArgumentList.Add(siteRoot.Value);

        // Place Pagefind output under <site>/pagefind/ — the canonical location its loader expects
        // when consumers write `import("/pagefind/pagefind.js")`. The OutputSubdirectory is reserved
        // for any future engine bookkeeping the runtime doesn't own.
        psi.ArgumentList.Add("--output-subdir");
        psi.ArgumentList.Add("pagefind");

        PagefindCliLogging.LogInvoking(logger, binary, siteRoot);

        using var process = new Process { StartInfo = psi };
        StringBuilder stdout = new();
        StringBuilder stderr = new();
        process.OutputDataReceived += (_, e) => AppendIfPresent(stdout, e.Data);
        process.ErrorDataReceived += (_, e) => AppendIfPresent(stderr, e.Data);

        try
        {
            process.Start();
        }
        catch (Exception ex) when (!options.StrictBinaryRequired)
        {
            PagefindCliLogging.LogStartFailed(logger, binary, ex.Message);
            return false;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await WaitForExitOrKillAsync(process, cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            PagefindCliLogging.LogFailed(logger, process.ExitCode, stderr.ToString());
            if (options.StrictBinaryRequired)
            {
                throw new InvalidOperationException(
                    $"Pagefind exited with code {process.ExitCode}. stderr: {stderr}");
            }

            return false;
        }

        PagefindCliLogging.LogSucceeded(logger, stdout.Length, stderr.Length);
        return true;
    }

    /// <summary>Waits for <paramref name="process"/> to exit or, on cancellation, kills the tree before re-throwing.</summary>
    /// <param name="process">Running process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes on exit.</returns>
    private static async Task WaitForExitOrKillAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process);
            throw;
        }
    }

    /// <summary>Kills <paramref name="process"/> and tolerates "already exited" without bubbling.</summary>
    /// <param name="process">Process handle.</param>
    private static void KillIfRunning(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // already exited
        }
    }

    /// <summary>Appends <paramref name="line"/> to <paramref name="sink"/> when non-null.</summary>
    /// <param name="sink">Destination buffer.</param>
    /// <param name="line">Captured line, or null when the stream signalled EOF.</param>
    private static void AppendIfPresent(StringBuilder sink, string? line)
    {
        if (line is null)
        {
            return;
        }

        sink.AppendLine(line);
    }

    /// <summary>Resolves the absolute path of the Pagefind binary to invoke, or null when no binary is locatable.</summary>
    /// <param name="explicitOverride">User-supplied override; tried first.</param>
    /// <returns>An absolute path to a binary that exists on disk, or null.</returns>
    private static string? ResolveBinaryPath(FilePath explicitOverride)
    {
        if (!explicitOverride.IsEmpty && File.Exists(explicitOverride.Value))
        {
            return explicitOverride.Value;
        }

        var rid = RuntimeInformation.RuntimeIdentifier;
        var fileName = OperatingSystem.IsWindows() ? "pagefind.exe" : "pagefind";
        var probe = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        if (File.Exists(probe))
        {
            return probe;
        }

        // Fall back to a generic-RID probe (linux-x64, osx-arm64, etc.) — covers the case where
        // RuntimeIdentifier reports something more specific (e.g. linux-musl-x64) but the package
        // ships only the broader RID folder.
        var fallback = Path.Combine(AppContext.BaseDirectory, "runtimes", FallbackRid(), "native", fileName);
        if (File.Exists(fallback))
        {
            return fallback;
        }

        // PATH lookup — last-resort developer convenience when natives haven't been bundled.
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < pathDirs.Length; i++)
        {
            var candidate = Path.Combine(pathDirs[i], fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Maps the host's specific RID (e.g. <c>linux-musl-x64</c>) to the broader RID folder we ship (<c>linux-x64</c>).</summary>
    /// <returns>A broad RID like <c>linux-x64</c>.</returns>
    private static string FallbackRid()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "x64",
        };

        if (OperatingSystem.IsWindows())
        {
            return "win-" + arch;
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx-" + arch;
        }

        return "linux-" + arch;
    }
}
