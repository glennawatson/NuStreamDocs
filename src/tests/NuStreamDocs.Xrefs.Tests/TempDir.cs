// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Xrefs.Tests;

/// <summary>Disposable temp-directory fixture.</summary>
internal sealed class TempDir : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
    /// <param name="root">Absolute path.</param>
    private TempDir(string root) => Root = root;

    /// <summary>Gets the absolute path of the temp root.</summary>
    public string Root { get; }

    /// <summary>Creates a fresh temp tree.</summary>
    /// <returns>New fixture; caller must dispose.</returns>
    public static TempDir Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "smkd-xrefs-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        return new(root);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
