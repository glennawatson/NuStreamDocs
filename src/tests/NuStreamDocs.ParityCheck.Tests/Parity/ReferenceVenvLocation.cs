// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Decides where the reference-engine virtual environment lives on disk.</summary>
internal static class ReferenceVenvLocation
{
    /// <summary>Environment variable that overrides the virtual environment directory.</summary>
    internal const string OverrideVariable = "NUSTREAMDOCS_PARITY_VENV";

    /// <summary>Directory name of the environment under the repository's ignored <c>artifacts</c> folder.</summary>
    private const string VenvName = "parity-venv";

    /// <summary>Solution file that marks the source root of the repository.</summary>
    private const string SolutionFile = "NuStreamDocs.slnx";

    /// <summary>Resolves the virtual environment directory.</summary>
    /// <param name="startDirectory">Directory inside the repository where the search for the repository root starts.</param>
    /// <returns>
    /// The override directory when <see cref="OverrideVariable"/> is set, otherwise <c>artifacts/parity-venv</c> under the repository root,
    /// otherwise a folder under the user's local application data.
    /// </returns>
    internal static string Resolve(string startDirectory)
    {
        var overridden = Environment.GetEnvironmentVariable(OverrideVariable);
        if (overridden is [_, ..])
        {
            return Path.GetFullPath(overridden);
        }

        var root = FindRepositoryRoot(startDirectory);
        return root is null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuStreamDocs", VenvName)
            : Path.Combine(root, "artifacts", VenvName);
    }

    /// <summary>Gets the path of the interpreter inside a virtual environment for the current operating system.</summary>
    /// <param name="venvDirectory">Virtual environment directory.</param>
    /// <returns>The interpreter path: <c>Scripts\python.exe</c> on Windows, <c>bin/python</c> elsewhere.</returns>
    internal static string InterpreterPath(string venvDirectory) =>
        OperatingSystem.IsWindows()
            ? Path.Combine(venvDirectory, "Scripts", "python.exe")
            : Path.Combine(venvDirectory, "bin", "python");

    /// <summary>Walks up from <paramref name="startDirectory"/> to the directory that contains <c>src/NuStreamDocs.slnx</c>.</summary>
    /// <param name="startDirectory">Directory to start from.</param>
    /// <returns>The repository root, or <see langword="null"/> when none contains the solution.</returns>
    private static string? FindRepositoryRoot(string startDirectory)
    {
        for (var directory = Path.GetFullPath(startDirectory); directory is [_, ..]; directory = Path.GetDirectoryName(directory))
        {
            if (File.Exists(Path.Combine(directory, "src", SolutionFile)))
            {
                return directory;
            }
        }

        return null;
    }
}
