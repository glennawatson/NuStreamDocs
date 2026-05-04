// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Tests;

/// <summary>Direct tests for OutputPathBuilder and EmbeddedResourceReader.</summary>
public class BuildPipelineHelperTests
{
    /// <summary>Non-md path passes through to the flat-url helper.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonMarkdownPathPassThrough()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "asset.css");
        await Assert.That(path.Value).EndsWith("asset.css");
    }

    /// <summary>index.md keeps a flat output path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndexMdStaysFlat()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "guide/index.md");
        await Assert.That(path.Value).EndsWith("index.html");
    }

    /// <summary>Non-index .md becomes "&lt;stem&gt;/index.html".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonIndexBecomesDirectoryUrl()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "guide/intro.md").Replace('\\', '/');
        await Assert.That(path.Value).EndsWith("guide/intro/index.html");
    }

    /// <summary>Flat-URL form swaps .md to .html.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FlatUrlSwapsExtension()
    {
        var path = OutputPathBuilder.ForFlatUrls("/out", "guide/intro.md").Replace('\\', '/');
        await Assert.That(path.Value).EndsWith("guide/intro.html");
    }

    /// <summary>EmbeddedResourceReader.Read throws when the resource name does not exist on the assembly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadEmbeddedThrowsOnMissing()
    {
        var asm = typeof(BuildPipelineHelperTests).Assembly;
        var src = ExtraAssetSource.Embedded(asm, "does-not-exist.bin", "out.bin");
        var ex = Assert.Throws<InvalidOperationException>(() => EmbeddedResourceReader.Read(src));
        await Assert.That(ex).IsNotNull();
    }
}
