// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Optimize.Tests;

/// <summary>Lifecycle method coverage for <c>OptimizePlugin</c> and <c>HtmlMinifyPlugin</c>.</summary>
public class OptimizePluginLifecycleTests
{
    /// <summary>OptimizePlugin.Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimizeName() =>
        await Assert.That(new OptimizePlugin().Name.SequenceEqual("optimize"u8)).IsTrue();

    /// <summary>OptimizePlugin.FinalizeAsync runs on an empty output dir.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptimizeFinalizeAsync()
    {
        using ScratchDir temp = new();
        await new OptimizePlugin().FinalizeAsync(new(temp.Root, []), CancellationToken.None);
    }

    /// <summary>HtmlMinifyPlugin.Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyName() =>
        await Assert.That(new HtmlMinifyPlugin().Name.SequenceEqual("html-minify"u8)).IsTrue();

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
