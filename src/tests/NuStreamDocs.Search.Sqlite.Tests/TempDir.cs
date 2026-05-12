// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Sqlite.Tests;

/// <summary>Disposable scratch directory under the temp path.</summary>
internal sealed class TempDir : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
    public TempDir()
    {
        Root = Path.Combine(Path.GetTempPath(), "smkd-sqlite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Gets the absolute path to the scratch root.</summary>
    public string Root { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // already gone
        }
    }
}
