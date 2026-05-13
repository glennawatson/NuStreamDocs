// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Tests;

/// <summary>Coverage for <see cref="ContentLoaderPlugin"/> and the builder extensions.</summary>
public class ContentLoaderPluginTests
{
    /// <summary>The plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new ContentLoaderPlugin([]).Name.SequenceEqual("content-loader"u8)).IsTrue();

    /// <summary>Running the discover hook feeds every loader's pages into the sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DiscoverFeedsSink()
    {
        FakeLoader one = new([new(new("a/x.md"), [.. "x"u8]), new(new("a/y.md"), [.. "y"u8])]);
        FakeLoader two = new([new(new("b/z.md"), [.. "z"u8])]);
        var plugin = new ContentLoaderPlugin([one, two]);

        SyntheticPageSink sink = new();
        BuildDiscoverContext context = new(default, default, [], sink);
        await plugin.DiscoverAsync(context, CancellationToken.None);

        var paths = sink.Snapshot().Select(p => p.RelativePath.Value).OrderBy(static p => p, StringComparer.Ordinal)
            .ToArray();
        await Assert.That(string.Join(",", paths)).IsEqualTo("a/x.md,a/y.md,b/z.md");
    }

    /// <summary>The builder extensions register the plugin and return the builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuilderExtensionsRegister()
    {
        FakeLoader loader = new([]);
        await Assert.That(new DocBuilder().UseContentLoader(loader)).IsNotNull();
        await Assert.That(new DocBuilder().UseContentLoaders(loader)).IsNotNull();
        await Assert.That(new DocBuilder().UseContentLoaders(NullLogger.Instance, loader)).IsNotNull();
    }

    /// <summary>A test loader that returns a fixed page set.</summary>
    private sealed class FakeLoader(SyntheticPage[] pages) : IContentLoader
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "fake"u8;

        /// <inheritdoc/>
        public ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
        {
            _ = context;
            _ = cancellationToken;
            return ValueTask.FromResult(pages);
        }
    }
}
