// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Search.Pagefind.Logging;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Locates and invokes the bundled <c>pagefind</c> CLI binary against a rendered site.</summary>
/// <remarks>
/// Resolution order: explicit <see cref="PagefindOptions.BinaryPath"/> override, then the per-RID native under <c>runtimes/&lt;rid&gt;/native/</c>, then PATH.
/// Missing-binary handling honors <see cref="PagefindOptions.StrictBinaryRequired"/> — log-and-skip by default, throw when strict.
/// </remarks>
public static class PagefindCli
{
    /// <summary>Stdio drain buffer size in bytes.</summary>
    private const int DrainBufferSize = 4 * 1024;

    /// <summary>Gets the Pagefind release this package targets; matches the binary version under <c>runtimes/</c>.</summary>
    public static string PinnedVersion => "1.5.2";

    /// <summary>Discovers and invokes Pagefind against <paramref name="siteRoot"/>.</summary>
    /// <param name="siteRoot">Absolute path to the rendered site directory.</param>
    /// <param name="options">Plugin options carrying the binary override + strict/run toggles.</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when Pagefind ran and exited cleanly; false when skipped (binary missing with non-strict, or <c>RunCli=false</c>).</returns>
    public static async Task<bool> RunAsync(
        DirectoryPath siteRoot,
        PagefindOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(siteRoot.Value);

        if (!options.RunCli)
        {
            return false;
        }

        var binary = ResolveBinaryPath(options.BinaryPath);
        return binary is { } binaryPath
            ? await InvokeAsync(binaryPath, siteRoot, options, logger, cancellationToken).ConfigureAwait(false)
            : HandleMissingBinary(logger);
    }

    /// <summary>Either throws a strict-mode error or logs a soft warning.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <returns>Always false (the soft path).</returns>
    private static bool HandleMissingBinary(ILogger logger)
    {
        PagefindCliLogging.LogBinaryMissing(logger, RuntimeInformation.RuntimeIdentifier);
        return false;
    }

    /// <summary>Spawns Pagefind, waits for exit, and reports success based on the exit code.</summary>
    /// <param name="binary">Resolved binary path.</param>
    /// <param name="siteRoot">Rendered site directory (passed via <c>--site</c>).</param>
    /// <param name="options">Plugin options (carries the strict toggle).</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True on exit-code-zero; false on tolerated failure (strict off).</returns>
    private static async Task<bool> InvokeAsync(
        FilePath binary,
        DirectoryPath siteRoot,
        PagefindOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        PagefindCliLogging.LogInvoking(logger, binary, siteRoot);

        using var process = new Process { StartInfo = CreateStartInfo(binary, siteRoot) };
        try
        {
            _ = process.Start();
        }
        catch (Exception ex) when (!options.StrictBinaryRequired)
        {
            PagefindCliLogging.LogStartFailed(logger, binary, ex.Message);
            return false;
        }

        // Drain both pipes concurrently so a full pipe cannot prevent the child from exiting.
        var stderrSink = new ArrayBufferWriter<byte>();
        var stdoutDrain = DrainStdoutAsync(process.StandardOutput.BaseStream, cancellationToken).AsTask();
        var stderrDrain = DrainIntoAsync(process.StandardError.BaseStream, stderrSink, cancellationToken).AsTask();
        var result = await WaitForCompletionAsync(process, stdoutDrain, stderrDrain, cancellationToken).ConfigureAwait(false);

        if (!HandleExit(result.ExitCode, result.Signal, stderrSink.WrittenSpan, options.StrictBinaryRequired, logger))
        {
            return false;
        }

        PagefindCliLogging.LogSucceeded(logger, ClampToInt(result.StdoutBytes), stderrSink.WrittenCount);
        return true;
    }

    /// <summary>Configures the Pagefind command for the rendered site.</summary>
    /// <param name="binary">Resolved executable path.</param>
    /// <param name="siteRoot">Rendered site directory.</param>
    /// <returns>The process launch configuration.</returns>
    private static ProcessStartInfo CreateStartInfo(in FilePath binary, in DirectoryPath siteRoot)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = binary.Value,
            WorkingDirectory = siteRoot.Value,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        startInfo.ArgumentList.Add("--site");
        startInfo.ArgumentList.Add(siteRoot.Value);
        startInfo.ArgumentList.Add("--output-subdir");
        startInfo.ArgumentList.Add("pagefind");
        startInfo.ArgumentList.Add("--quiet");
        return startInfo;
    }

    /// <summary>Waits for output and process termination, killing the process when canceled.</summary>
    /// <param name="process">Running Pagefind process.</param>
    /// <param name="stdoutDrain">Output drain reporting the number of bytes read.</param>
    /// <param name="stderrDrain">Error output drain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code, termination signal when available, and stdout byte count.</returns>
    /// <exception cref="OperationCanceledException">The invocation was canceled.</exception>
    private static async Task<(int ExitCode, PosixSignal? Signal, long StdoutBytes)> WaitForCompletionAsync(
        Process process,
        Task<long> stdoutDrain,
        Task stderrDrain,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(stdoutDrain, stderrDrain).ConfigureAwait(false);
            var stdoutBytes = await stdoutDrain.ConfigureAwait(false);
#if NET11_0_OR_GREATER
            var exitStatus = await process.WaitForExitStatusAsync(cancellationToken).ConfigureAwait(false);
            return (exitStatus.ExitCode, exitStatus.Signal, stdoutBytes);
#else
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return (process.ExitCode, null, stdoutBytes);
#endif
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process);
            throw;
        }
    }

    /// <summary>Reports process failure according to the strict-mode setting.</summary>
    /// <param name="exitCode">Process exit code.</param>
    /// <param name="signal">Termination signal when reported by the runtime.</param>
    /// <param name="stderr">Captured stderr bytes.</param>
    /// <param name="strict">Whether failures throw.</param>
    /// <param name="logger">Diagnostic logger.</param>
    /// <returns>True for a normal zero exit code; otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The process failed and strict mode is enabled.</exception>
    private static bool HandleExit(int exitCode, PosixSignal? signal, ReadOnlySpan<byte> stderr, bool strict, ILogger logger)
    {
        if (exitCode is 0 && signal is null)
        {
            return true;
        }

        var stderrText = new ApiCompatString(Encoding.UTF8.GetString(stderr));
        if (signal is { } terminationSignal)
        {
            PagefindCliLogging.LogTerminated(logger, terminationSignal, stderrText);
        }
        else
        {
            PagefindCliLogging.LogFailed(logger, exitCode, stderrText);
        }

        if (!strict)
        {
            return false;
        }

        throw new InvalidOperationException(signal is { } failureSignal
            ? StringCompose.Concat("Pagefind terminated by signal ", failureSignal.ToString(), ". stderr: ", stderrText)
            : StringCompose.ConcatInt("Pagefind exited with code ", exitCode, StringCompose.Concat(". stderr: ", stderrText)));
    }

    /// <summary>Drains <paramref name="stream"/> into <paramref name="sink"/> a pooled chunk at a time.</summary>
    /// <param name="stream">Source stream.</param>
    /// <param name="sink">Byte sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the stream signals EOF.</returns>
    private static async ValueTask DrainIntoAsync(
        Stream stream,
        IBufferWriter<byte> sink,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(DrainBufferSize);
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                sink.Write(buffer.AsSpan(0, read));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Drains <paramref name="stream"/> and returns the byte count.</summary>
    /// <param name="stream">Source stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total bytes read.</returns>
    private static async ValueTask<long> DrainStdoutAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(DrainBufferSize);
        try
        {
            long total = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return total;
                }

                total += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Saturating cast from <see cref="long"/> to <see cref="int"/>.</summary>
    /// <param name="value">Source value.</param>
    /// <returns>Clamped value.</returns>
    private static int ClampToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)value;

    /// <summary>Kills <paramref name="process"/> tolerating "already exited".</summary>
    /// <param name="process">Process handle.</param>
    private static void KillIfRunning(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // already exited
        }
    }

    /// <summary>Resolves the absolute path of the Pagefind binary to invoke, or null on miss.</summary>
    /// <param name="explicitOverride">User-supplied override; tried first.</param>
    /// <returns>An absolute path that exists on disk, or null.</returns>
    private static FilePath? ResolveBinaryPath(in FilePath explicitOverride)
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

        // Broader-RID fallback for hosts that report a more specific RID (e.g. linux-musl-x64
        // when only linux-x64 is bundled).
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

    /// <summary>Maps the host's specific RID to the broader RID folder we ship.</summary>
    /// <returns>A broad RID like <c>linux-x64</c>.</returns>
    private static string FallbackRid()
    {
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "x64"
        };

        if (OperatingSystem.IsWindows())
        {
            return $"win-{arch}";
        }

        return OperatingSystem.IsMacOS() ? $"osx-{arch}" : $"linux-{arch}";
    }
}
