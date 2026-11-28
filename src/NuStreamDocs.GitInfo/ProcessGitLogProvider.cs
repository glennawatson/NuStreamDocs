// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using NuStreamDocs.Common;

namespace NuStreamDocs.GitInfo;

/// <summary>Executes a single batched <c>git log</c> command over the documentation directory.</summary>
[DebuggerDisplay("ProcessGitLogProvider: {_gitExecutable}")]
public sealed class ProcessGitLogProvider : IGitLogProvider
{
    /// <summary>Git executable name or path.</summary>
    private readonly string _gitExecutable;

    /// <summary>Initializes a new instance of the <see cref="ProcessGitLogProvider"/> class using <c>git</c> from <c>PATH</c>.</summary>
    public ProcessGitLogProvider()
        : this("git")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ProcessGitLogProvider"/> class with a custom executable name.</summary>
    /// <param name="gitExecutable">Git executable path or name.</param>
    internal ProcessGitLogProvider(string gitExecutable) => _gitExecutable = gitExecutable;

    /// <summary>Gets the shared default provider instance.</summary>
    public static ProcessGitLogProvider Instance { get; } = new();

    /// <inheritdoc/>
    public async ValueTask<byte[]> ReadGitLogAsync(DirectoryPath inputRoot, CancellationToken cancellationToken)
    {
        if (inputRoot.IsEmpty || !inputRoot.Exists())
        {
            return [];
        }

        var startInfo = CreateStartInfo(inputRoot.Value);
        try
        {
            using var process = Process.Start(startInfo)!;
            byte[] logBytes;
            MemoryStream output = new();
            await using (output.ConfigureAwait(false))
            {
                var drainStderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
                await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await drainStderr.ConfigureAwait(false);
                var succeeded = await ProcessCompat.WaitForZeroExitAsync(process, cancellationToken).ConfigureAwait(false);
                logBytes = succeeded ? output.ToArray() : [];
            }

            return logBytes;
        }
        catch (Win32Exception)
        {
            return [];
        }
    }

    /// <summary>Builds the batched <c>git log</c> <see cref="ProcessStartInfo"/>.</summary>
    /// <param name="workingPath">Repository directory path.</param>
    /// <returns>Configured start info.</returns>
    private ProcessStartInfo CreateStartInfo(string workingPath)
    {
        ProcessStartInfo startInfo = new(_gitExecutable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workingPath);
        startInfo.ArgumentList.Add("log");
        startInfo.ArgumentList.Add("--format=%x1e%ad%x1f%an");
        startInfo.ArgumentList.Add("--date=short");
        startInfo.ArgumentList.Add("--relative");
        startInfo.ArgumentList.Add("--name-only");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(".");
        return startInfo;
    }
}
