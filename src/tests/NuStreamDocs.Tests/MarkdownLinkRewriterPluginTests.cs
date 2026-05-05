// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Links;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Lifecycle / registration tests for <c>MarkdownLinkRewriterPlugin</c>.</summary>
public class MarkdownLinkRewriterPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new MarkdownLinkRewriterPlugin().Name.SequenceEqual("markdown-link-rewriter"u8)).IsTrue();

    /// <summary>PostRender rewrites a <c>.md</c> href to <c>.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesMdHrefToHtml()
    {
        MarkdownLinkRewriterPlugin plugin = new();
        await plugin.ConfigureAsync(new("/in", "/out", [], new()), CancellationToken.None);

        var output = RunPostRender(plugin, "<a href=\"guide/intro.md\">x</a>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("guide/intro");
    }

    /// <summary>HTML without a <c>.md</c> href is signalled as no-op via <see cref="MarkdownLinkRewriterPlugin.NeedsRewrite"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoMdHref()
    {
        var html = "<p>plain</p>"u8;
        MarkdownLinkRewriterPlugin plugin = new();

        await Assert.That(plugin.NeedsRewrite(html)).IsFalse();
    }

    /// <summary>Caller-supplied <c>useDirectoryUrls=true</c> overrides the config's false value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExplicitDirectoryUrlsOverridesConfig()
    {
        MarkdownLinkRewriterPlugin plugin = new(useDirectoryUrls: true);
        BuildConfigureContext configCtx = new("/in", "/out", [], new()) { UseDirectoryUrls = false };
        await plugin.ConfigureAsync(configCtx, CancellationToken.None);

        var output = RunPostRender(plugin, "<a href=\"intro.md\">x</a>"u8);
        var rewritten = Encoding.UTF8.GetString(output);
        await Assert.That(rewritten).DoesNotContain("intro.html");
    }

    /// <summary>UseMarkdownLinks() registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMarkdownLinksRegisters() =>
        await Assert.That(new DocBuilder().UseMarkdownLinks()).IsTypeOf<DocBuilder>();

    /// <summary>UseMarkdownLinks(bool) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMarkdownLinksWithToggleRegisters() =>
        await Assert.That(new DocBuilder().UseMarkdownLinks(true)).IsTypeOf<DocBuilder>();

    /// <summary>UseMarkdownLinks rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMarkdownLinksRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderLinksExtensions.UseMarkdownLinks(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseMarkdownLinks(bool) rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMarkdownLinksToggleRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderLinksExtensions.UseMarkdownLinks(null!, true));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(MarkdownLinkRewriterPlugin plugin, ReadOnlySpan<byte> html)
    {
        ArrayBufferWriter<byte> output = new(64);
        PagePostRenderContext ctx = new("p.md", default, html, output);
        plugin.PostRender(in ctx);
        return [.. output.WrittenSpan];
    }
}
