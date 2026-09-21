// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>Finds a Python interpreter on the machine that the pinned reference packages support.</summary>
internal static class PythonProbe
{
    /// <summary>The oldest interpreter version the pinned reference packages support.</summary>
    internal static readonly Version MinimumVersion = new(3, 10);

    /// <summary>Prefix of the line an interpreter prints for <c>--version</c>.</summary>
    private const string VersionPrefix = "Python ";

    /// <summary>Command name of the interpreter on most systems.</summary>
    private const string Python3Command = "python3";

    /// <summary>Command name of the interpreter where only the unversioned name is installed.</summary>
    private const string PythonCommand = "python";

    /// <summary>Command name of the Windows launcher.</summary>
    private const string WindowsLauncherCommand = "py";

    /// <summary>Launcher argument that selects the newest Python 3 installation.</summary>
    private const string WindowsLauncherPython3Argument = "-3";

    /// <summary>Newest minor version whose versioned command name is probed.</summary>
    private const int NewestProbedMinor = 14;

    /// <summary>Number of version components read: major, minor and patch.</summary>
    private const int ComponentCount = 3;

    /// <summary>Number of version components that must be present: major and minor.</summary>
    private const int RequiredComponentCount = 2;

    /// <summary>Index of the patch component.</summary>
    private const int PatchIndex = 2;

    /// <summary>Longest time one <c>--version</c> probe may take before the candidate is abandoned.</summary>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Probes each candidate interpreter in order and returns the first that is new enough.</summary>
    /// <returns>The chosen interpreter, or none with a description of every candidate probed.</returns>
    internal static async Task<PythonProbeResult> FindAsync()
    {
        var candidates = Candidates(OperatingSystem.IsWindows());
        var attempts = new List<string>(candidates.Length);
        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            string[] arguments = [.. candidate.LeadingArguments, "--version"];
            var result = await ProcessRunner.RunAsync(candidate.FileName, arguments, ProbeTimeout).ConfigureAwait(false);
            if (!TryParseVersion(result.CombinedOutput, out var version))
            {
                attempts.Add(result.Started ? $"{candidate.Display} (no version reported, exit code {result.ExitCode})" : $"{candidate.Display} (not found)");
                continue;
            }

            if (version < MinimumVersion)
            {
                attempts.Add($"{candidate.Display} (version {version}, older than {MinimumVersion})");
                continue;
            }

            attempts.Add($"{candidate.Display} (version {version})");
            return new(candidate with { Version = version }, [.. attempts]);
        }

        return new(null, [.. attempts]);
    }

    /// <summary>Extracts the <c>major.minor.patch</c> version from the text printed by <c>python --version</c>.</summary>
    /// <param name="output">Text printed by the interpreter.</param>
    /// <param name="version">The parsed version when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a version was found.</returns>
    internal static bool TryParseVersion(string output, out Version version)
    {
        version = new();
        var text = output.AsSpan().Trim();
        var index = text.IndexOf(VersionPrefix, StringComparison.Ordinal);
        if (index < 0)
        {
            return false;
        }

        Span<int> parts = stackalloc int[ComponentCount];
        var count = ReadComponents(text[(index + VersionPrefix.Length)..], parts);
        if (count < RequiredComponentCount)
        {
            return false;
        }

        version = new(parts[0], parts[1], count > PatchIndex ? parts[PatchIndex] : 0);
        return true;
    }

    /// <summary>Lists the interpreter command lines to try, most likely first, for the given operating system family.</summary>
    /// <param name="windows">Whether the candidates are for Windows.</param>
    /// <returns>Candidate command lines.</returns>
    internal static PythonInterpreter[] Candidates(bool windows) => windows ? WindowsCandidates() : UnixCandidates();

    /// <summary>Reads dot-separated numeric components from the start of <paramref name="text"/>.</summary>
    /// <param name="text">Text that starts with the first component.</param>
    /// <param name="parts">Destination for the components; its length bounds how many are read.</param>
    /// <returns>The number of components read.</returns>
    private static int ReadComponents(ReadOnlySpan<char> text, Span<int> parts)
    {
        var count = 0;
        while (count < parts.Length)
        {
            var digits = text.IndexOfAnyExceptInRange('0', '9');
            digits = digits < 0 ? text.Length : digits;
            if (digits is 0 || !int.TryParse(text[..digits], out parts[count]))
            {
                break;
            }

            count++;
            text = text[digits..];
            if (text is not ['.', ..])
            {
                break;
            }

            text = text[1..];
        }

        return count;
    }

    /// <summary>Lists the Windows candidates: the launcher first because the bare names can be Microsoft Store stubs.</summary>
    /// <returns>Candidate command lines.</returns>
    private static PythonInterpreter[] WindowsCandidates() =>
    [
        new(WindowsLauncherCommand, [WindowsLauncherPython3Argument], $"{WindowsLauncherCommand} {WindowsLauncherPython3Argument}", new()),
        Bare(PythonCommand),
        Bare(Python3Command),
    ];

    /// <summary>Lists the Linux and macOS candidates: the generic names, then versioned names from newest down to the minimum.</summary>
    /// <returns>Candidate command lines.</returns>
    private static PythonInterpreter[] UnixCandidates()
    {
        var candidates = new List<PythonInterpreter>(NewestProbedMinor);
        candidates.Add(Bare(Python3Command));
        candidates.Add(Bare(PythonCommand));
        for (var minor = NewestProbedMinor; minor >= MinimumVersion.Minor; minor--)
        {
            candidates.Add(Bare($"{Python3Command}.{minor}"));
        }

        return [.. candidates];
    }

    /// <summary>Creates a candidate that is launched by its own name with no leading arguments.</summary>
    /// <param name="command">Executable name.</param>
    /// <returns>The candidate.</returns>
    private static PythonInterpreter Bare(string command) => new(command, [], command, new());
}
