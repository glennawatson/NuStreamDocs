// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Creates the reference-engine virtual environment and installs the pinned packages into it.</summary>
internal static class ReferenceVenv
{
    /// <summary>Longest time to wait for another process that is preparing the same environment.</summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(20);

    /// <summary>Longest time virtual environment creation may take.</summary>
    private static readonly TimeSpan CreateTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Longest time the package install may take.</summary>
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(15);

    /// <summary>Longest time listing installed packages may take.</summary>
    private static readonly TimeSpan ListTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Delay between attempts to take the cross-process lock.</summary>
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>Makes sure <paramref name="venvDirectory"/> holds a virtual environment with exactly the pinned packages, creating or repairing it when needed.</summary>
    /// <param name="python">System interpreter used to create the environment.</param>
    /// <param name="venvDirectory">Directory of the environment.</param>
    /// <returns>The environment's own interpreter path.</returns>
    /// <exception cref="InvalidOperationException">The environment could not be created or the packages could not be installed; the message carries the tool output.</exception>
    internal static async Task<string> EnsureAsync(PythonInterpreter python, string venvDirectory)
    {
        var parent = Path.GetDirectoryName(venvDirectory);
        if (parent is [_, ..])
        {
            _ = Directory.CreateDirectory(parent);
        }

        await using var venvLock = await AcquireLockAsync($"{venvDirectory}.lock").ConfigureAwait(false);
        var interpreter = ReferenceVenvLocation.InterpreterPath(venvDirectory);

        var installed = File.Exists(interpreter) ? await ListPackagesAsync(interpreter).ConfigureAwait(false) : null;
        installed ??= await CreateAsync(python, venvDirectory).ConfigureAwait(false);
        if (Matches(installed))
        {
            return interpreter;
        }

        await InstallAsync(interpreter, venvDirectory).ConfigureAwait(false);
        var verified = await ListPackagesAsync(interpreter).ConfigureAwait(false);
        return verified is not null && Matches(verified)
            ? interpreter
            : throw new InvalidOperationException($"The packages in {venvDirectory} do not match the pins ({string.Join(", ", ReferencePins.Requirements())}) after installing.");
    }

    /// <summary>Normalizes a Python package name so that <c>PyMdown_Extensions</c> and <c>pymdown-extensions</c> compare equal.</summary>
    /// <param name="name">Package name as reported by the installer.</param>
    /// <returns>Lower-case name with dashes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string NormalizePackageName(string name) =>
        name.ToLowerInvariant().Replace('_', '-').Replace('.', '-');

    /// <summary>Determines whether every pinned package is installed at exactly its pinned version.</summary>
    /// <param name="installed">Installed versions keyed by normalized package name.</param>
    /// <returns><see langword="true"/> when all pins match.</returns>
    internal static bool Matches(Dictionary<string, string> installed)
    {
        for (var i = 0; i < ReferencePins.Packages.Length; i++)
        {
            var pin = ReferencePins.Packages[i];
            if (!installed.TryGetValue(pin.Name, out var version) || !string.Equals(version, pin.Version, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Takes an exclusive lock so parallel test processes never prepare the same environment at once.</summary>
    /// <param name="lockPath">Lock file path.</param>
    /// <returns>The open lock file; disposing it releases the lock.</returns>
    private static async Task<FileStream> AcquireLockAsync(string lockPath)
    {
        FileStream? handle = null;
        var start = Stopwatch.GetTimestamp();
        while (handle is null)
        {
            try
            {
                handle = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (Stopwatch.GetElapsedTime(start) < LockTimeout)
            {
                await Task.Delay(LockRetryDelay).ConfigureAwait(false);
            }
        }

        return handle;
    }

    /// <summary>Creates the virtual environment, clearing it first only when it already is one.</summary>
    /// <param name="python">System interpreter.</param>
    /// <param name="venvDirectory">Directory to create.</param>
    /// <returns>The packages installed in the new environment.</returns>
    /// <exception cref="InvalidOperationException">The environment could not be created or has no working package installer.</exception>
    private static async Task<Dictionary<string, string>> CreateAsync(PythonInterpreter python, string venvDirectory)
    {
        var existing = File.Exists(Path.Combine(venvDirectory, "pyvenv.cfg"));
        string[] arguments = existing
            ? [.. python.LeadingArguments, "-m", "venv", "--clear", venvDirectory]
            : [.. python.LeadingArguments, "-m", "venv", venvDirectory];
        var result = await ProcessRunner.RunAsync(python.FileName, arguments, CreateTimeout).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Creating the virtual environment at {venvDirectory} with '{python.Display} -m venv' failed (exit code {result.ExitCode}).\n{result.CombinedOutput}");
        }

        var interpreter = ReferenceVenvLocation.InterpreterPath(venvDirectory);
        return await ListPackagesAsync(interpreter).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The new virtual environment at {venvDirectory} has no working package installer ({interpreter}).");
    }

    /// <summary>Installs the pinned packages into the environment.</summary>
    /// <param name="interpreter">The environment's interpreter.</param>
    /// <param name="venvDirectory">Environment directory, used in diagnostics.</param>
    /// <returns>A task that completes when the install has finished.</returns>
    /// <exception cref="InvalidOperationException">The installer failed; the message carries its output.</exception>
    private static async Task InstallAsync(string interpreter, string venvDirectory)
    {
        string[] arguments = ["-m", "pip", "install", "--quiet", "--only-binary=zensical", .. ReferencePins.Requirements()];
        var result = await ProcessRunner.RunAsync(interpreter, arguments, InstallTimeout).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Installing {string.Join(' ', ReferencePins.Requirements())} into {venvDirectory} failed (exit code {result.ExitCode}).\n{result.CombinedOutput}");
        }
    }

    /// <summary>Lists the packages installed in the environment.</summary>
    /// <param name="interpreter">The environment's interpreter.</param>
    /// <returns>Installed versions keyed by normalized package name, or <see langword="null"/> when the installer does not run.</returns>
    private static async Task<Dictionary<string, string>?> ListPackagesAsync(string interpreter)
    {
        var result = await ProcessRunner.RunAsync(interpreter, ["-m", "pip", "list", "--format=json"], ListTimeout).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            Dictionary<string, string> installed = [with(StringComparer.Ordinal)];
            foreach (var package in document.RootElement.EnumerateArray())
            {
                var name = package.GetProperty("name").GetString();
                var version = package.GetProperty("version").GetString();
                if (name is not null && version is not null)
                {
                    installed[NormalizePackageName(name)] = version;
                }
            }

            return installed;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
