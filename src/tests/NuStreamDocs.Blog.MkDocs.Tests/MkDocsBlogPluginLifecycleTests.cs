// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog.MkDocs.Tests;

/// <summary>Lifecycle method coverage for <c>MkDocsBlogPlugin</c>.</summary>
public class MkDocsBlogPluginLifecycleTests
{
    /// <summary>OnConfigureAsync runs without error against an empty blog tree.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnConfigureAsync()
    {
        using var temp = new ScratchDir();
        Directory.CreateDirectory(Path.Combine(temp.Root, "blog", "posts"));
        var plugin = new MkDocsBlogPlugin(new("blog", "Blog"));
        await plugin.OnConfigureAsync(new(default, temp.Root, "/out", []), CancellationToken.None);
    }

    /// <summary>OnRenderPageAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderPageAsync()
    {
        var plugin = new MkDocsBlogPlugin(new("blog", "Blog"));
        await plugin.OnRenderPageAsync(new("p.md", default, new(8)), CancellationToken.None);
    }

    /// <summary>OnFinaliseAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinaliseAsync()
    {
        var plugin = new MkDocsBlogPlugin(new("blog", "Blog"));
        await plugin.OnFinaliseAsync(new("/out"), CancellationToken.None);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-mb-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch directory.</summary>
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
}
