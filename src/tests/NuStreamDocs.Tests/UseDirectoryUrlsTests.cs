// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Links;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end coverage for the <c>use_directory_urls</c> toggle: output paths, link rewriting, and DocBuilder fluent.</summary>
public class UseDirectoryUrlsTests
{
    /// <summary>Platform-specific separator used to build expected outputs.</summary>
    private static readonly char Sep = Path.DirectorySeparatorChar;

    /// <summary>Flat-URL form: <c>guide/foo.md</c> → <c>out/guide/foo.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FlatUrlsMapNonIndexFiles()
    {
        var path = OutputPathBuilder.ForFlatUrls("/out", "guide/foo.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}guide/foo.html");
    }

    /// <summary>Directory-URL form: <c>guide/foo.md</c> → <c>out/guide/foo/index.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsMapNonIndexFilesToIndexHtml()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "guide/foo.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}guide/foo{Sep}index.html");
    }

    /// <summary>Directory-URL form preserves <c>index.md</c> at its original location.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsKeepIndexMdFlat()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "guide/index.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}guide/index.html");
    }

    /// <summary>Directory-URL form passes non-markdown assets through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsPassNonMarkdownThrough()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "assets/style.css");
        await Assert.That(path).IsEqualTo($"/out{Sep}assets/style.css");
    }

    /// <summary>Top-level <c>foo.md</c> works with directory URLs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsHandleRootStem()
    {
        var path = OutputPathBuilder.ForDirectoryUrls("/out", "about.md");
        await Assert.That(path).IsEqualTo($"/out{Sep}about{Sep}index.html");
    }

    /// <summary>Link rewriter (flat): <c>about.md</c> → <c>about.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterFlatRewritesMdToHtml()
    {
        var html = "<a href=\"about.md\">about</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: false);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"about.html\">about</a>");
    }

    /// <summary>Link rewriter (directory): <c>about.md</c> → <c>about/</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryAppendsSlash()
    {
        var html = "<a href=\"about.md\">about</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"about/\">about</a>");
    }

    /// <summary>Link rewriter (directory) collapses <c>guide/index.md</c> to <c>guide/</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryCollapsesIndexMd()
    {
        var html = "<a href=\"guide/index.md\">guide</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"guide/\">guide</a>");
    }

    /// <summary>Link rewriter (directory) collapses bare <c>index.md</c> to the directory root (empty path).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryCollapsesBareIndexMd()
    {
        var html = "<a href=\"index.md\">home</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"\">home</a>");
    }

    /// <summary>Link rewriter preserves anchors on directory-URL targets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryPreservesAnchors()
    {
        var html = "<a href=\"guide.md#install\">install</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"guide/#install\">install</a>");
    }

    /// <summary>Link rewriter preserves query strings on directory-URL targets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryPreservesQuery()
    {
        var html = "<a href=\"search.md?q=foo\">search</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"search/?q=foo\">search</a>");
    }

    /// <summary>Link rewriter passes external URLs through under both modes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterPassesExternalUrlsThrough()
    {
        var html = "<a href=\"https://example.com/foo.md\">e</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"https://example.com/foo.md\">e</a>");
    }

    /// <summary>Link rewriter collapses <c>index.md</c> with a fragment to <c>#section</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkRewriterDirectoryIndexWithAnchor()
    {
        var html = "<a href=\"index.md#top\">top</a>"u8;
        var rewritten = MarkdownLinkRewriter.Rewrite(html, useDirectoryUrls: true);
        await Assert.That(Encoding.UTF8.GetString(rewritten)).IsEqualTo("<a href=\"#top\">top</a>");
    }

    /// <summary>DocBuilder fluent toggle is captured on the builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DocBuilderFluentEnablesDirectoryUrls()
    {
        var builder = new DocBuilder();
        await Assert.That(builder.UseDirectoryUrlsEnabled).IsFalse();
        builder.UseDirectoryUrls();
        await Assert.That(builder.UseDirectoryUrlsEnabled).IsTrue();
    }

    /// <summary>DocBuilder fluent overload accepts a literal toggle.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DocBuilderFluentOverloadAcceptsLiteralToggle()
    {
        var builder = new DocBuilder().UseDirectoryUrls(enabled: false);
        await Assert.That(builder.UseDirectoryUrlsEnabled).IsFalse();
    }
}
