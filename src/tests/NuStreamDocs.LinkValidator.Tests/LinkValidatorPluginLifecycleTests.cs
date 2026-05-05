// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Lifecycle method coverage for <c>LinkValidatorPlugin</c>.</summary>
public class LinkValidatorPluginLifecycleTests
{
    /// <summary>ConfigureAsync runs without error.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureAsync() =>
        await new LinkValidatorPlugin().ConfigureAsync(new("/in", "/out", [], new()), CancellationToken.None);

    /// <summary>ResolveAsync drives the validator against an empty index.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolveAsyncEmpty()
    {
        using ScratchDir temp = new();
        LinkValidatorPlugin plugin = new();
        await plugin.ConfigureAsync(new(temp.Root, temp.Root, [], new()), CancellationToken.None);
        await plugin.ResolveAsync(new(temp.Root, []), CancellationToken.None);
        await Assert.That(plugin.LastDiagnostics).IsNotNull();
    }

    /// <summary>RunAsync against an empty directory returns no diagnostics.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncEmpty()
    {
        using ScratchDir temp = new();
        var diags = await new LinkValidatorPlugin().RunAsync(temp.Root, CancellationToken.None);
        await Assert.That(diags.Length).IsEqualTo(0);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-lv-" + Guid.NewGuid().ToString("N"));
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
