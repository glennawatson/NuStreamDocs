// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Runs external programs without a shell and captures their output.</summary>
internal static class ProcessRunner
{
    /// <summary>Runs <paramref name="fileName"/> with <paramref name="arguments"/> and no standard input.</summary>
    /// <param name="fileName">Executable to start.</param>
    /// <param name="arguments">Arguments passed one by one, never through a shell.</param>
    /// <param name="timeout">Longest time to wait before the process is killed.</param>
    /// <returns>The captured result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Task<ProcessResult> RunAsync(string fileName, string[] arguments, TimeSpan timeout) =>
        RunAsync(fileName, arguments, null, timeout);

    /// <summary>Runs <paramref name="fileName"/> with <paramref name="arguments"/> and writes <paramref name="standardInput"/> to it.</summary>
    /// <param name="fileName">Executable to start.</param>
    /// <param name="arguments">Arguments passed one by one, never through a shell.</param>
    /// <param name="standardInput">UTF-8 text written to standard input, or <see langword="null"/> for none.</param>
    /// <param name="timeout">Longest time to wait before the process is killed.</param>
    /// <returns>The captured result.</returns>
    internal static async Task<ProcessResult> RunAsync(string fileName, string[] arguments, string? standardInput, TimeSpan timeout)
    {
        var startInfo = CreateStartInfo(fileName, arguments, standardInput is not null);

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception exception)
        {
            return new(false, -1, string.Empty, exception.Message);
        }

        if (process is null)
        {
            return new(false, -1, string.Empty, "the process could not be started");
        }

        using (process)
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (standardInput is not null)
            {
                await WriteStandardInputAsync(process, standardInput).ConfigureAwait(false);
            }

            using var timeoutSource = new CancellationTokenSource(timeout);
            ProcessExit exit;
            try
            {
                exit = await WaitForExitAsync(process, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                process.Kill(true);
                await process.WaitForExitAsync().ConfigureAwait(false);
                return new(true, -1, await standardOutput.ConfigureAwait(false), $"timed out after {timeout.TotalSeconds:0} seconds\n{await standardError.ConfigureAwait(false)}");
            }

            return new(true, exit.ExitCode, await standardOutput.ConfigureAwait(false), $"{exit.Note}{await standardError.ConfigureAwait(false)}");
        }
    }

    /// <summary>Waits for the process to exit and reports how it ended, including the terminating signal where the runtime exposes it.</summary>
    /// <param name="process">The running process.</param>
    /// <param name="cancellationToken">Token that abandons the wait.</param>
    /// <returns>The exit information.</returns>
    private static async Task<ProcessExit> WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
#if NET11_0_OR_GREATER
        var status = await process.WaitForExitStatusAsync(cancellationToken).ConfigureAwait(false);
        return new(status.ExitCode, status.Signal is { } signal ? $"terminated by signal {signal}\n" : string.Empty);
#else
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new(process.ExitCode, string.Empty);
#endif
    }

    /// <summary>Builds the start information for a redirected, shell-free process running UTF-8 Python tooling.</summary>
    /// <param name="fileName">Executable to start.</param>
    /// <param name="arguments">Arguments passed one by one.</param>
    /// <param name="redirectInput">Whether standard input is redirected.</param>
    /// <returns>The start information.</returns>
    private static ProcessStartInfo CreateStartInfo(string fileName, string[] arguments, bool redirectInput)
    {
        var encoding = new UTF8Encoding(false);
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
        };

        for (var i = 0; i < arguments.Length; i++)
        {
            startInfo.ArgumentList.Add(arguments[i]);
        }

        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        startInfo.Environment["PIP_DISABLE_PIP_VERSION_CHECK"] = "1";
        return startInfo;
    }

    /// <summary>Writes the request and closes standard input; a process that exited early reports its own failure through its exit code.</summary>
    /// <param name="process">The running process.</param>
    /// <param name="text">Text to write.</param>
    /// <returns>A task that completes when the input is closed.</returns>
    private static async Task WriteStandardInputAsync(Process process, string text)
    {
        try
        {
            await process.StandardInput.WriteAsync(text).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The process closed its input before reading everything; its exit code and standard error explain why.
        }
    }
}
