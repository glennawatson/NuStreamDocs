// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.Feed.Tests;

/// <summary>Coverage for <see cref="RssAtomReader"/> and <see cref="FeedContentLoader"/>.</summary>
public class FeedContentLoaderTests
{
    /// <summary>Number of items in the RSS fixture.</summary>
    private const int RssItemCount = 2;

    /// <summary>Gets a two-item RSS 2.0 feed.</summary>
    private static ReadOnlySpan<byte> Rss =>
        """
        <?xml version="1.0"?><rss version="2.0"><channel><title>Blog</title>
        <item><title>First Post</title><link>https://blog.test/first</link>
        <pubDate>Mon, 06 May 2026 12:00:00 GMT</pubDate><guid>https://blog.test/first</guid>
        <description>Summary one</description></item>
        <item><title>Second Post</title><link>https://blog.test/second</link>
        <description>Summary two</description></item></channel></rss>
        """u8;

    /// <summary>Gets a single-entry Atom feed with self and alternate links.</summary>
    private static ReadOnlySpan<byte> Atom =>
        """
        <?xml version="1.0"?><feed xmlns="http://www.w3.org/2005/Atom"><title>Blog</title>
        <entry><title>Atom Entry</title><link rel="self" href="https://a.test/feed"/>
        <link rel="alternate" href="https://a.test/post"/><updated>2026-05-06T12:00:00Z</updated>
        <id>urn:uuid:1</id><content type="html">&lt;p&gt;Body&lt;/p&gt;</content></entry></feed>
        """u8;

    /// <summary>The RSS reader extracts each item with its title, link, date, and body.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsRss()
    {
        var items = RssAtomReader.Read([.. Rss]);
        await Assert.That(items.Length).IsEqualTo(RssItemCount);
        await Assert.That(Encoding.UTF8.GetString(items[0].Title)).IsEqualTo("First Post");
        await Assert.That(Encoding.UTF8.GetString(items[0].Link)).IsEqualTo("https://blog.test/first");
        await Assert.That(Encoding.UTF8.GetString(items[0].ContentHtml)).IsEqualTo("Summary one");
    }

    /// <summary>The Atom reader prefers the <c>rel="alternate"</c> link and reads the content element.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsAtomPreferringAlternateLink()
    {
        var items = RssAtomReader.Read([.. Atom]);
        await Assert.That(items.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(items[0].Link)).IsEqualTo("https://a.test/post");
        await Assert.That(Encoding.UTF8.GetString(items[0].ContentHtml)).IsEqualTo("<p>Body</p>");
    }

    /// <summary>Invalid XML throws a <see cref="ContentLoaderException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InvalidXmlThrows() =>
        await Assert.That(static () => _ = RssAtomReader.Read("<not xml"u8.ToArray()))
            .Throws<ContentLoaderException>();

    /// <summary>The loader fetches the feed and produces one page per item with frontmatter and body.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoaderProducesPages()
    {
        using var fixture = new FeedHttpClientFixture([.. Rss], HttpStatusCode.OK);
        var loader = new FeedContentLoader(
            (UrlPath)"https://blog.test/feed.xml",
            (PathSegment)"blog/external",
            () => fixture.Client,
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(RssItemCount);
        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("blog/external/first-post.md");
        var md = Encoding.UTF8.GetString(pages[0].MarkdownBytes);
        await Assert.That(md).Contains("title: \"First Post\"");
        await Assert.That(md).Contains("external_url: \"https://blog.test/first\"");
        await Assert.That(md).Contains("Summary one");
    }

    /// <summary>Duplicate slugs get a numeric suffix so routes stay unique.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DuplicateSlugsAreDisambiguated()
    {
        using var fixture = new FeedHttpClientFixture(
            [.. """
                <rss version="2.0"><channel>
                <item><title>Same Title</title><description>a</description></item>
                <item><title>Same Title</title><description>b</description></item></channel></rss>
                """u8],
            HttpStatusCode.OK);
        var loader = new FeedContentLoader(
            (UrlPath)"https://x.test/f",
            (PathSegment)"p",
            () => fixture.Client,
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new(default), CancellationToken.None);

        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("p/same-title.md");
        await Assert.That(pages[1].RelativePath.Value).IsEqualTo("p/same-title-2.md");
    }

    /// <summary>Default loaders reuse their transport without carrying response cookies between feeds.</summary>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultClientsReuseConnectionsWithoutCookies(CancellationToken cancellationToken)
    {
        const int RequestCount = 2;
        await using var server = new LoopbackFeedServer(RequestCount, [.. Rss]);
        for (var i = 0; i < RequestCount; i++)
        {
            var loader = new FeedContentLoader(server.Endpoint, "external");
            var pages = await loader.LoadAsync(new(default), cancellationToken);
            await Assert.That(pages[0].RelativePath.Value).IsEqualTo("external/first-post.md");
        }

        var (connections, cookies) = await server.Requests;
        await Assert.That(connections).IsEqualTo(1);
        for (var i = 0; i < cookies.Length; i++)
        {
            await Assert.That(cookies[i]).IsFalse();
        }
    }

    /// <summary>Success and failure preserve factory clients and invoke the factory for each load.</summary>
    /// <param name="fails">Whether the response has an unsuccessful status.</param>
    /// <param name="cancellationToken">Token that cancels the test.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SuppliedClientsRemainCallerOwned(bool fails, CancellationToken cancellationToken)
    {
        const int LoadCount = 2;
        var status = fails ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
        using var fixture = new FeedHttpClientFixture([.. Rss], status);
        var invocations = 0;
        HttpClient CreateClient()
        {
            invocations++;
            return fixture.Client;
        }

        var endpoint = new Uri("https://factory.test/feed.xml");
        var loader = new FeedContentLoader(endpoint.AbsoluteUri, "external", CreateClient, NullLogger.Instance);
        for (var i = 0; i < LoadCount; i++)
        {
            if (fails)
            {
                await Assert.That(async () => _ = await loader.LoadAsync(new(default), cancellationToken)).Throws<ContentLoaderException>();
                continue;
            }

            _ = await loader.LoadAsync(new(default), cancellationToken);
        }

        await Assert.That(invocations).IsEqualTo(LoadCount);
        using var response = await fixture.Client.GetAsync(endpoint, cancellationToken);
        await Assert.That(response.StatusCode).IsEqualTo(status);
    }
}
