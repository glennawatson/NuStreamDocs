// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Config;
using NuStreamDocs.Links;

namespace NuStreamDocs.Tests;

/// <summary>Lifecycle / registration tests for <c>MarkdownLinkRewriterPlugin</c>.</summary>
public class MarkdownLinkRewriterPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new MarkdownLinkRewriterPlugin().Name).IsEqualTo("markdown-link-rewriter");

    /// <summary>OnRenderPageAsync rewrites a <c>.md</c> href to <c>.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesMdHrefToHtml()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write(Encoding.UTF8.GetBytes("<a href=\"guide/intro.md\">x</a>"));

        var plugin = new MarkdownLinkRewriterPlugin();
        var config = new MkDocsConfig("Site", null, "material", []);
        await plugin.OnConfigureAsync(new(config, "/in", "/out", []), CancellationToken.None);
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);

        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("guide/intro");
    }

    /// <summary>HTML without a <c>.md</c> href is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoMdHref()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        const string Html = "<p>plain</p>";
        sink.Write(Encoding.UTF8.GetBytes(Html));

        var plugin = new MarkdownLinkRewriterPlugin();
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);

        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
    }

    /// <summary>Caller-supplied <c>useDirectoryUrls=true</c> overrides the config's false value.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExplicitDirectoryUrlsOverridesConfig()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write(Encoding.UTF8.GetBytes("<a href=\"intro.md\">x</a>"));

        var plugin = new MarkdownLinkRewriterPlugin(useDirectoryUrls: true);
        var config = new MkDocsConfig("Site", null, "material", [], UseDirectoryUrls: false);
        await plugin.OnConfigureAsync(new(config, "/in", "/out", []), CancellationToken.None);
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).DoesNotContain("intro.html");
    }

    /// <summary>OnFinaliseAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinaliseAsyncNoOp()
    {
        var plugin = new MarkdownLinkRewriterPlugin();
        await plugin.OnFinaliseAsync(new("/out"), CancellationToken.None);
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
}
