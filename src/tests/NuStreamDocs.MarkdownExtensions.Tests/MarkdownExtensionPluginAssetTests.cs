// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Admonitions;
using NuStreamDocs.MarkdownExtensions.CheckList;
using NuStreamDocs.MarkdownExtensions.Details;
using NuStreamDocs.MarkdownExtensions.Tabs;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Static-asset / head-extra contract tests for plugins that ship CSS.</summary>
public class MarkdownExtensionPluginAssetTests
{
    /// <summary>AdmonitionPlugin ships its stylesheet via IStaticAssetProvider.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AdmonitionStaticAssets()
    {
        var assets = new AdmonitionPlugin().StaticAssets;
        await Assert.That(assets.Length).IsEqualTo(1);
        await Assert.That(assets[0].Path.Value).Contains("admonition");
        await Assert.That(assets[0].Bytes.Length).IsGreaterThan(0);
    }

    /// <summary>AdmonitionPlugin emits its head-extra link tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AdmonitionHeadExtra()
    {
        ArrayBufferWriter<byte> sink = new(64);
        new AdmonitionPlugin().WriteHeadExtra(sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("admonition");
    }

    /// <summary>AdmonitionPlugin head-extra rejects null sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AdmonitionHeadExtraRejectsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new AdmonitionPlugin().WriteHeadExtra(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>DetailsPlugin ships its stylesheet.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DetailsStaticAssets() =>
        await Assert.That(new DetailsPlugin().StaticAssets.Length).IsGreaterThan(0);

    /// <summary>DetailsPlugin emits head extras.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DetailsHeadExtra()
    {
        ArrayBufferWriter<byte> sink = new(64);
        new DetailsPlugin().WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsGreaterThan(0);
    }

    /// <summary>CheckListPlugin ships its stylesheet.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CheckListStaticAssets() =>
        await Assert.That(new CheckListPlugin().StaticAssets.Length).IsGreaterThan(0);

    /// <summary>CheckListPlugin emits head extras.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CheckListHeadExtra()
    {
        ArrayBufferWriter<byte> sink = new(64);
        new CheckListPlugin().WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsGreaterThan(0);
    }

    /// <summary>TabsPlugin ships its stylesheet.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabsStaticAssets() =>
        await Assert.That(new TabsPlugin().StaticAssets.Length).IsGreaterThan(0);

    /// <summary>TabsPlugin emits head extras.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabsHeadExtra()
    {
        ArrayBufferWriter<byte> sink = new(64);
        new TabsPlugin().WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsGreaterThan(0);
    }
}
