// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Nav.Tests;

/// <summary>Disposable temp-directory fixture for nav-tree tests.</summary>
internal sealed class TempDocsTree : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TempDocsTree"/> class.</summary>
    /// <param name="root">Absolute path to the freshly-created temp root.</param>
    private TempDocsTree(string root)
    {
        Root = root;
        Output = Path.Combine(root, "_site");
    }

    /// <summary>Gets the absolute path of the temp root (also the docs input root).</summary>
    public string Root { get; }

    /// <summary>Gets the absolute path of the per-fixture output directory.</summary>
    public string Output { get; }

    /// <summary>Creates a fresh temp tree under <c>Path.GetTempPath</c>.</summary>
    /// <returns>A new fixture; caller must dispose.</returns>
    public static TempDocsTree Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "smkd-nav-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
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
            // Best-effort cleanup; the OS will reap the temp dir eventually.
        }
    }
}
