// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Toc.Tests;

/// <summary>End-to-end tests exercising <c>TocPlugin</c> via its post-render hook.</summary>
public class TocPluginTests
{
    /// <summary>Plugin rewrites headings with ids + permalinks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesHeadingsAndAddsPermalinks()
    {
        var output = RunPostRender(new(), "<h1>Page</h1><h2>Section</h2>"u8);
        var rendered = Encoding.UTF8.GetString(output);
        await Assert.That(rendered).Contains("<h1 id=\"page\">");
        await Assert.That(rendered).Contains("<h2 id=\"section\">");
        await Assert.That(rendered).Contains("class=\"headerlink\"");
    }

    /// <summary>Marker is replaced with the rendered TOC fragment.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SubstitutesTocMarker()
    {
        var output = RunPostRender(new(), "<aside><!--@@toc@@--></aside><h2>Alpha</h2><h2>Beta</h2>"u8);
        var rendered = Encoding.UTF8.GetString(output);
        await Assert.That(rendered).DoesNotContain("<!--@@toc@@-->");
        await Assert.That(rendered).Contains("md-nav--secondary");
        await Assert.That(rendered).Contains("href=\"#alpha\"");
        await Assert.That(rendered).Contains("href=\"#beta\"");
    }

    /// <summary>An empty render buffer is signalled as no-op via NeedsRewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyBufferIsNoOp() =>
        await Assert.That(new TocPlugin().NeedsRewrite(default)).IsFalse();

    /// <summary>HTML without any heading is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoHeadingsLeavesHtmlUnchanged()
    {
        const string Body = "<p>just a paragraph</p>";
        var output = RunPostRender(new(), Encoding.UTF8.GetBytes(Body));
        await Assert.That(Encoding.UTF8.GetString(output)).IsEqualTo(Body);
    }

    /// <summary>Marker substitution active but marker absent leaves the rewrite output intact.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkerSubstituteWithoutMarkerLeavesBodyAlone()
    {
        var plugin = new TocPlugin(TocOptions.Default with { MarkerSubstitute = true });
        var output = RunPostRender(plugin, "<h1>Heading</h1><p>body</p>"u8);
        var rendered = Encoding.UTF8.GetString(output);
        await Assert.That(rendered).Contains("<h1");
        await Assert.That(rendered).DoesNotContain("<!--@@toc@@-->");
    }

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(TocPlugin plugin, ReadOnlySpan<byte> html)
    {
        var output = new ArrayBufferWriter<byte>(256);
        var ctx = new PagePostRenderContext("page.md", default, html, output);
        plugin.PostRender(in ctx);
        return [.. output.WrittenSpan];
    }
}
