// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Branch-coverage tests for StaticAssetComposer.WriteAll.</summary>
public class StaticAssetComposerTests
{
    /// <summary>Plugins implementing IStaticAssetProvider have their assets written under outputRoot.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ProviderAssetsWrittenToOutputRoot()
    {
        using var dir = new TempDir();
        var plugin = new TestProvider(("a/b.css", "AAA"u8.ToArray()), ("c.txt", "CCC"u8.ToArray()));

        StaticAssetComposer.WriteAll([plugin], dir.Root);

        await Assert.That(File.Exists(Path.Combine(dir.Root, "a", "b.css"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(dir.Root, "c.txt"))).IsTrue();
    }

    /// <summary>Plugins that don't implement IStaticAssetProvider are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonProviderPluginIgnored()
    {
        using var dir = new TempDir();
        var nonProvider = new NonProvider();
        StaticAssetComposer.WriteAll([nonProvider], dir.Root);
        await Assert.That(Directory.GetFiles(dir.Root)).IsEmpty();
    }

    /// <summary>An empty plugin list is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyPluginListNoOp()
    {
        using var dir = new TempDir();
        StaticAssetComposer.WriteAll([], dir.Root);
        await Assert.That(Directory.GetFiles(dir.Root)).IsEmpty();
    }

    /// <summary>Null plugin array throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullPluginsThrows() =>
        await Assert.That(() => StaticAssetComposer.WriteAll(null!, "/tmp")).Throws<ArgumentNullException>();

    /// <summary>Empty outputRoot throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyOutputRootThrows() =>
        await Assert.That(() => StaticAssetComposer.WriteAll([], string.Empty)).Throws<ArgumentException>();

    /// <summary>Test plugin that exposes static assets.</summary>
    /// <param name="assets">Asset entries.</param>
    private sealed class TestProvider(params (string Path, byte[] Bytes)[] assets) : IDocPlugin, IStaticAssetProvider
    {
        /// <inheritdoc/>
        public string Name => "test-provider";

        /// <inheritdoc/>
        public (string Path, byte[] Bytes)[] StaticAssets { get; } = assets;

        /// <inheritdoc/>
        public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    /// <summary>Plugin that does not implement IStaticAssetProvider.</summary>
    private sealed class NonProvider : IDocPlugin
    {
        /// <inheritdoc/>
        public string Name => "non-provider";

        /// <inheritdoc/>
        public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-sac-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch root.</summary>
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
