// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Toc.Tests;

/// <summary>End-to-end tests exercising <c>TocPlugin</c> via <c>IDocPlugin.OnRenderPageAsync</c>.</summary>
public class TocPluginTests
{
    /// <summary>Plugin rewrites headings with ids + permalinks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesHeadingsAndAddsPermalinks()
    {
        var bytes = "<h1>Page</h1><h2>Section</h2>"u8.ToArray();
        var sink = new ArrayBufferWriter<byte>(128);
        sink.Write(bytes);

        var plugin = new TocPlugin();
        var ctx = new PluginRenderContext("page.md", bytes, sink);
        await plugin.OnRenderPageAsync(ctx, CancellationToken.None);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("<h1 id=\"page\">");
        await Assert.That(output).Contains("<h2 id=\"section\">");
        await Assert.That(output).Contains("class=\"headerlink\"");
    }

    /// <summary>Marker is replaced with the rendered TOC fragment.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SubstitutesTocMarker()
    {
        byte[] bytes = [.. "<aside><!--@@toc@@--></aside><h2>Alpha</h2><h2>Beta</h2>"u8];
        var sink = new ArrayBufferWriter<byte>(256);
        sink.Write(bytes);

        var plugin = new TocPlugin();
        var ctx = new PluginRenderContext("page.md", bytes, sink);
        await plugin.OnRenderPageAsync(ctx, CancellationToken.None);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).DoesNotContain("<!--@@toc@@-->");
        await Assert.That(output).Contains("md-nav--secondary");
        await Assert.That(output).Contains("href=\"#alpha\"");
        await Assert.That(output).Contains("href=\"#beta\"");
    }

    /// <summary>An empty render buffer is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyBufferReturnsImmediately()
    {
        var plugin = new TocPlugin();
        var html = new ArrayBufferWriter<byte>();
        var ctx = new PluginRenderContext("page.md", default, html);
        await plugin.OnRenderPageAsync(ctx, CancellationToken.None);
        await Assert.That(html.WrittenCount).IsEqualTo(0);
    }

    /// <summary>HTML without any heading is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoHeadingsLeavesHtmlUnchanged()
    {
        var plugin = new TocPlugin();
        var html = new ArrayBufferWriter<byte>();
        const string Body = "<p>just a paragraph</p>";
        html.Write(Encoding.UTF8.GetBytes(Body));
        var ctx = new PluginRenderContext("page.md", default, html);
        await plugin.OnRenderPageAsync(ctx, CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(html.WrittenSpan)).IsEqualTo(Body);
    }

    /// <summary>Marker substitution active but marker absent leaves the rewrite output intact.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkerSubstituteWithoutMarkerLeavesBodyAlone()
    {
        var plugin = new TocPlugin(TocOptions.Default with { MarkerSubstitute = true });
        var html = new ArrayBufferWriter<byte>();
        html.Write("<h1>Heading</h1><p>body</p>"u8);
        var ctx = new PluginRenderContext("page.md", default, html);
        await plugin.OnRenderPageAsync(ctx, CancellationToken.None);
        var rendered = Encoding.UTF8.GetString(html.WrittenSpan);
        await Assert.That(rendered).Contains("<h1");
        await Assert.That(rendered).DoesNotContain("<!--@@toc@@-->");
    }
}
