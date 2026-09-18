// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Common;

namespace NuStreamDocs.Building;

/// <summary>Rejects multiple index pages in the same source directory during a build.</summary>
internal sealed class IndexPageRegistry
{
    /// <summary>Initial number of source directories expected to contain landing pages.</summary>
    private const int InitialCapacity = 32;

    /// <summary>Index source path registered for each directory.</summary>
    private readonly ConcurrentDictionary<DirectoryPath, FilePath> _pages = new(Environment.ProcessorCount, InitialCapacity);

    /// <summary>Reserves a directory's index page before it can be rendered or served from the build cache.</summary>
    /// <param name="relativePath">Source path relative to the docs root.</param>
    /// <exception cref="InvalidOperationException">Another index page is registered for the same directory.</exception>
    internal void Register(in FilePath relativePath)
    {
        var path = relativePath.AsSpan();
        var lastSeparator = path.LastIndexOfAny('/', '\\');
        if (!path[(lastSeparator + 1)..].Equals("index.md", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var directory = relativePath.Replace('\\', '/').Directory;
        if (_pages.TryAdd(directory, relativePath))
        {
            return;
        }

        throw new InvalidOperationException(StringCompose.Concat(
            "Index pages '",
            _pages[directory].Value,
            "' and '",
            relativePath.Value,
            "' share a source directory. Rename one of these pages to avoid conflicting URLs and output files."));
    }
}
