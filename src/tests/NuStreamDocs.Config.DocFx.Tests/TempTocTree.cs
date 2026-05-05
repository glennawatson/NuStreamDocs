// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.DocFx.Tests;

/// <summary>Disposable scratch directory used by docfx-toc reader tests.</summary>
internal sealed class TempTocTree : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TempTocTree"/> class.</summary>
    /// <param name="root">Absolute path of the temporary docs root.</param>
    private TempTocTree(string root) => Root = root;

    /// <summary>Gets the absolute path of the temporary docs root.</summary>
    public string Root { get; }

    /// <summary>Allocates a fresh temporary directory.</summary>
    /// <returns>The fixture.</returns>
    public static TempTocTree Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "nustreamdocs-toc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new(path);
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
            // Best-effort cleanup; transient locks should not fail the test.
        }
        catch (UnauthorizedAccessException)
        {
            // Same — leave temp residue rather than masking real failures.
        }
    }
}
