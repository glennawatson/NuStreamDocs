// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Lifecycle method coverage for <c>AutorefsPlugin</c>.</summary>
public class AutorefsPluginLifecycleTests
{
    /// <summary>OnConfigureAsync is a no-op that completes synchronously.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnConfigureAsync()
    {
        await new AutorefsPlugin().OnConfigureAsync(new(default, "/in", "/out", []), CancellationToken.None);
    }

    /// <summary>OnRenderPageAsync collects heading IDs into the registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderPageAsync()
    {
        var plugin = new AutorefsPlugin();
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write("<h1 id=\"hello\">Hello</h1>"u8);
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
    }

    /// <summary>OnFinaliseAsync runs even when the registry is empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinaliseAsyncEmpty()
    {
        using var temp = new TempDir();
        await new AutorefsPlugin().OnFinaliseAsync(new(temp.Root), CancellationToken.None);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-ar-" + Guid.NewGuid().ToString("N"));
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
