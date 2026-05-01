// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Optimise.Tests;

/// <summary>Lifecycle method coverage for <c>OptimisePlugin</c> and <c>HtmlMinifyPlugin</c>.</summary>
public class OptimisePluginLifecycleTests
{
    /// <summary>OptimisePlugin.Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimiseName() =>
        await Assert.That(new OptimisePlugin().Name).IsEqualTo("optimise");

    /// <summary>OptimisePlugin.OnConfigureAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimiseOnConfigureAsync() => await new OptimisePlugin().OnConfigureAsync(new(default, "/in", "/out", []), CancellationToken.None);

    /// <summary>OptimisePlugin.OnRenderPageAsync no-ops on plain HTML.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimiseOnRenderPageAsync()
    {
        var sink = new ArrayBufferWriter<byte>(16);
        sink.Write("<p>x</p>"u8);
        await new OptimisePlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
    }

    /// <summary>OptimisePlugin.OnFinaliseAsync runs on an empty output dir.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimiseOnFinaliseAsync()
    {
        using var temp = new ScratchDir();
        await new OptimisePlugin().OnFinaliseAsync(new(temp.Root), CancellationToken.None);
    }

    /// <summary>HtmlMinifyPlugin.OnConfigureAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyOnConfigureAsync() => await new HtmlMinifyPlugin().OnConfigureAsync(new(default, "/in", "/out", []), CancellationToken.None);

    /// <summary>HtmlMinifyPlugin.OnFinaliseAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyOnFinaliseAsync() => await new HtmlMinifyPlugin().OnFinaliseAsync(new("/out"), CancellationToken.None);

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-opt-" + Guid.NewGuid().ToString("N"));
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
