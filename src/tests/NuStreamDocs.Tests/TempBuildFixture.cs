// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Tests;

/// <summary>Disposable input/output directory pair for pipeline tests.</summary>
internal sealed class TempBuildFixture : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TempBuildFixture"/> class.</summary>
    /// <param name="root">Absolute path to the root of the fixture.</param>
    private TempBuildFixture(string root)
    {
        Root = root;
        Input = Path.Combine(root, "docs");
        Output = Path.Combine(root, "site");
        Directory.CreateDirectory(Input);
    }

    /// <summary>Gets the fixture root directory.</summary>
    public string Root { get; }

    /// <summary>Gets the absolute path to the input docs directory.</summary>
    public string Input { get; }

    /// <summary>Gets the absolute path to the output site directory.</summary>
    public string Output { get; }

    /// <summary>Creates a fresh fixture under <c>Path.GetTempPath</c>.</summary>
    /// <returns>A new fixture; caller must dispose.</returns>
    public static TempBuildFixture Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "smkd-build-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
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
