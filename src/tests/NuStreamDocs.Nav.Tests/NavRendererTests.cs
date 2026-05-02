// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Nav.Tests;

/// <summary>Behavior tests for nav rendering, including <c>navigation.prune</c>.</summary>
public class NavRendererTests
{
    /// <summary>The full renderer emits every page even when the active page is deep in the tree.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FullRendererEmitsEveryNode()
    {
        var html = await RenderAsync(prune: false, currentPage: "guide/intro.html");
        await Assert.That(html.Contains("guide/intro.html", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("blog/post.html", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>The pruned renderer drops sections outside the active branch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrunedRendererCollapsesSectionsOutsideActiveBranch()
    {
        var html = await RenderAsync(prune: true, currentPage: "guide/intro.html");

        await Assert.That(html.Contains("guide/intro.html", StringComparison.Ordinal)).IsTrue();

        // Blog section's child page should be hidden when pruning while the
        // active page is in guide/. The "Blog" label still appears as a
        // collapsed section header.
        await Assert.That(html.Contains("blog/post.html", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>The active page is flagged with <c>md-nav__link--active</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ActiveLeafGetsActiveClass()
    {
        var html = await RenderAsync(prune: false, currentPage: "guide/intro.html");
        await Assert.That(html.Contains("md-nav__link--active", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Builds the standard 2-section fixture and runs the plugin's per-page render path.</summary>
    /// <param name="prune">Prune mode flag.</param>
    /// <param name="currentPage">URL of the page being rendered.</param>
    /// <returns>The rendered HTML for the page.</returns>
    private static async Task<string> RenderAsync(bool prune, string currentPage)
    {
        using var fixture = TempDocsTree.Create();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "guide"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "blog"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Index");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide", "intro.md"), "# Intro");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "blog", "post.md"), "# Post");

        var options = NavOptions.Default with { Prune = prune };
        var plugin = new NavPlugin(options);
        var configureContext = new PluginConfigureContext(default, fixture.Root, fixture.Output, [plugin]);
        await plugin.OnConfigureAsync(configureContext, CancellationToken.None);

        var page = new ArrayBufferWriter<byte>();

        // Surrogate "themed" payload — just the marker and trailing tag.
        page.Write("<nav>"u8);
        page.Write(Encoding.UTF8.GetBytes(NavPlugin.NavMarker));
        page.Write("</nav>"u8);

        var sourcePath = currentPage.Replace(".html", ".md", StringComparison.Ordinal);
        var renderContext = new PluginRenderContext(sourcePath, ReadOnlyMemory<byte>.Empty, page);
        await plugin.OnRenderPageAsync(renderContext, CancellationToken.None);

        return Encoding.UTF8.GetString(page.WrittenSpan);
    }
}
