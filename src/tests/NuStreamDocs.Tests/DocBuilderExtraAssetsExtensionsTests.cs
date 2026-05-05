// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Tests;

/// <summary>Tests for <c>DocBuilderExtraAssetsExtensions</c>.</summary>
public class DocBuilderExtraAssetsExtensionsTests
{
    /// <summary>AddExtraCss(file) registers a single source.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraCssFileRegisters()
    {
        DocBuilder builder = new();
        var result = builder.AddExtraCss("style.css");
        var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        await Assert.That(plugin.StaticAssets).IsNotNull();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>AddExtraCss(params) registers multiple sources.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraCssParamsRegisters()
    {
        DocBuilder builder = new();
        var result = builder.AddExtraCss("a.css", "b.css", "c.css");
        var plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        await Assert.That(plugin.StaticAssets).IsNotNull();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>AddExtraCssInline accepts an inline payload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraCssInline()
    {
        DocBuilder builder = new();
        var result = builder.AddExtraCssInline("inline.css", [.. "body{}"u8]);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>AddExtraCssEmbedded registers an embedded resource.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraCssEmbedded()
    {
        DocBuilder builder = new();
        var result = builder.AddExtraCssEmbedded(typeof(DocBuilderExtraAssetsExtensionsTests).Assembly, "missing.resource", "x.css");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>AddExtraCssLink(url) registers an external link.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraCssLink()
    {
        DocBuilder builder = new();
        var result = builder.AddExtraCssLink("https://x.test/a.css");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>AddExtraCssLink(params) registers multiple external hrefs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraCssLinkParams()
    {
        DocBuilder builder = new();
        var result = builder.AddExtraCssLink("https://x.test/a.css", "https://x.test/b.css");
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>AddExtraJs covers the full surface.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddExtraJsAllShapes()
    {
        DocBuilder builder = new();
        await Assert.That(builder.AddExtraJs("x.js")).IsSameReferenceAs(builder);
        await Assert.That(builder.AddExtraJs("a.js", "b.js")).IsSameReferenceAs(builder);
        await Assert.That(builder.AddExtraJsInline("inline.js", [.. "var x;"u8])).IsSameReferenceAs(builder);
        await Assert.That(builder.AddExtraJsEmbedded(typeof(DocBuilderExtraAssetsExtensionsTests).Assembly, "missing.js", "embed.js")).IsSameReferenceAs(builder);
        await Assert.That(builder.AddExtraJsLink("https://x.test/a.js")).IsSameReferenceAs(builder);
        await Assert.That(builder.AddExtraJsLink("https://x.test/a.js", "https://x.test/b.js")).IsSameReferenceAs(builder);
    }

    /// <summary>Each AddExtra entry rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullBuilderGuards()
    {
        var ex1 = Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraCss(null!, (Common.FilePath)"x.css"));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraCssInline(null!, "x.css", []));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraCssEmbedded(null!, typeof(DocBuilderExtraAssetsExtensionsTests).Assembly, "r", "x.css"));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraCssLink(null!, "x"));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraJs(null!, (Common.FilePath)"x.js"));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraJsInline(null!, "x.js", []));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraJsEmbedded(null!, typeof(DocBuilderExtraAssetsExtensionsTests).Assembly, "r", "x.js"));
        Assert.Throws<ArgumentNullException>(static () => DocBuilderExtraAssetsExtensions.AddExtraJsLink(null!, "x"));
        await Assert.That(ex1).IsNotNull();
    }
}
