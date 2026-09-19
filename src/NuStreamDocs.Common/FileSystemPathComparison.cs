// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>Selects path comparison rules for a directory's filesystem.</summary>
public static class FileSystemPathComparison
{
    /// <summary>Determines whether path comparisons in a directory should ignore case.</summary>
    /// <param name="directory">An existing writable directory on the target filesystem.</param>
    /// <returns>The standard ordinal comparer matching the directory's case sensitivity.</returns>
    /// <remarks>Call once for an operation and reuse the returned comparer for its path lookups.</remarks>
    public static StringComparer GetComparer(in DirectoryPath directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory.Value);
        ApiCompatString filename = StringCompose.Concat(Path.GetRandomFileName(), "a");
        var probe = directory.File(filename);
        var alternative = directory.File(filename.Value!.ToUpperInvariant());
        using var handle = File.OpenHandle(probe, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        try
        {
            return File.Exists(alternative) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        }
        finally
        {
            File.Delete(probe);
        }
    }
}
