// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Disposable docs/site directory pair used by the theme tests.</summary>
internal sealed class TempBuildTree : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TempBuildTree"/> class.</summary>
    /// <param name="root">Absolute path to the fixture root.</param>
    private TempBuildTree(string root)
    {
        Root = root;
        Docs = Path.Combine(root, "docs");
        Site = Path.Combine(root, "site");
        Directory.CreateDirectory(Docs);
    }

    /// <summary>Gets the fixture root directory.</summary>
    public string Root { get; }

    /// <summary>Gets the absolute path to the input docs directory.</summary>
    public string Docs { get; }

    /// <summary>Gets the absolute path to the output site directory.</summary>
    public string Site { get; }

    /// <summary>Creates a fresh fixture under <c>Path.GetTempPath</c>.</summary>
    /// <returns>A new fixture; caller must dispose.</returns>
    public static TempBuildTree Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "smkd-md3-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
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
