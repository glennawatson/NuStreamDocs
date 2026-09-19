// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Building;

/// <summary>Removes recorded page outputs that a successful build no longer produces.</summary>
internal static class BuildOutputCleanup
{
    /// <summary>Removes obsolete owned outputs while retaining current pages and authored assets.</summary>
    /// <param name="previous">Output paths recorded by the preceding build.</param>
    /// <param name="current">Output paths produced by this build.</param>
    /// <param name="comparer">Cached equality rules for the output filesystem.</param>
    /// <param name="shell">Build roots.</param>
    internal static void RemoveObsolete(FilePath[] previous, FilePath[] current, IEqualityComparer<FilePath> comparer, in BuildPhaseShell shell)
    {
        if (previous is [])
        {
            return;
        }

        HashSet<FilePath> retained = [with(current.Length, comparer)];
        for (var i = 0; i < current.Length; i++)
        {
            _ = retained.Add(Path.GetFullPath(shell.OutputRoot.File(current[i].Replace('\\', '/'))));
        }

        for (var i = 0; i < previous.Length; i++)
        {
            if (!previous[i].IsEmpty)
            {
                RemoveIfObsolete(shell.OutputRoot.File(previous[i].Replace('\\', '/')), retained, comparer, shell);
            }
        }
    }

    /// <summary>Removes one obsolete page when its path stays inside the output tree.</summary>
    /// <param name="candidate">Recorded output file.</param>
    /// <param name="retained">Outputs owned by the current build.</param>
    /// <param name="comparer">Output filesystem path comparison.</param>
    /// <param name="shell">Build roots.</param>
    private static void RemoveIfObsolete(FilePath candidate, HashSet<FilePath> retained, IEqualityComparer<FilePath> comparer, in BuildPhaseShell shell)
    {
        FilePath full = Path.GetFullPath(candidate);
        var relative = shell.OutputRoot.Relative(full);
        var path = relative.AsSpan();
        if (Path.IsPathRooted(relative)
            || path.SequenceEqual("..")
            || path.StartsWith("../", StringComparison.Ordinal)
            || path.StartsWith("..\\", StringComparison.Ordinal)
            || retained.Contains(full)
            || !File.Exists(full)
            || File.Exists(shell.InputRoot.File(relative)))
        {
            return;
        }

        DirectoryPath root = Path.GetFullPath(shell.OutputRoot);
        var parent = full.Directory;
        while (!comparer.Equals(parent.Value, root.Value))
        {
            if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) is not 0)
            {
                return;
            }

            parent = Path.GetDirectoryName(parent.Value)!;
        }

        File.Delete(full);
    }
}
