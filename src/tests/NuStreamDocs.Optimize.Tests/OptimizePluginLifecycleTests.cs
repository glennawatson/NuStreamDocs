// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Optimize.Tests;

/// <summary>Lifecycle method coverage for <c>OptimizePlugin</c> and <c>HtmlMinifyPlugin</c>.</summary>
public class OptimizePluginLifecycleTests
{
    /// <summary>OptimizePlugin.Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimizeName() =>
        await Assert.That(new OptimizePlugin().Name.SequenceEqual("optimize"u8)).IsTrue();

    /// <summary>OptimizePlugin.OnConfigureAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimizeOnConfigureAsync() => await new OptimizePlugin().OnConfigureAsync(new("/in", "/out", []), CancellationToken.None);

    /// <summary>OptimizePlugin.OnRenderPageAsync no-ops on plain HTML.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimizeOnRenderPageAsync()
    {
        var sink = new ArrayBufferWriter<byte>(16);
        sink.Write("<p>x</p>"u8);
        await new OptimizePlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
    }

    /// <summary>OptimizePlugin.OnFinalizeAsync runs on an empty output dir.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimizeOnFinalizeAsync()
    {
        using var temp = new ScratchDir();
        await new OptimizePlugin().OnFinalizeAsync(new(temp.Root), CancellationToken.None);
    }

    /// <summary>HtmlMinifyPlugin.OnConfigureAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyOnConfigureAsync() => await new HtmlMinifyPlugin().OnConfigureAsync(new("/in", "/out", []), CancellationToken.None);

    /// <summary>HtmlMinifyPlugin.OnFinalizeAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyOnFinalizeAsync() => await new HtmlMinifyPlugin().OnFinalizeAsync(new("/out"), CancellationToken.None);

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
