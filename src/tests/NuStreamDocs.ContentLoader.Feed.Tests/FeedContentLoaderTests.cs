// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.ContentLoader;

namespace NuStreamDocs.ContentLoader.Feed.Tests;

/// <summary>Coverage for <see cref="RssAtomReader"/> and <see cref="FeedContentLoader"/>.</summary>
public class FeedContentLoaderTests
{
    /// <summary>A two-item RSS 2.0 feed.</summary>
    private const string Rss =
        "<?xml version=\"1.0\"?><rss version=\"2.0\"><channel><title>Blog</title>" +
        "<item><title>First Post</title><link>https://blog.test/first</link>" +
        "<pubDate>Mon, 06 May 2026 12:00:00 GMT</pubDate><guid>https://blog.test/first</guid>" +
        "<description>Summary one</description></item>" +
        "<item><title>Second Post</title><link>https://blog.test/second</link>" +
        "<description>Summary two</description></item></channel></rss>";

    /// <summary>A single-entry Atom feed with self and alternate links.</summary>
    private const string Atom =
        "<?xml version=\"1.0\"?><feed xmlns=\"http://www.w3.org/2005/Atom\"><title>Blog</title>" +
        "<entry><title>Atom Entry</title><link rel=\"self\" href=\"https://a.test/feed\"/>" +
        "<link rel=\"alternate\" href=\"https://a.test/post\"/><updated>2026-05-06T12:00:00Z</updated>" +
        "<id>urn:uuid:1</id><content type=\"html\">&lt;p&gt;Body&lt;/p&gt;</content></entry></feed>";

    /// <summary>The RSS reader extracts each item with its title, link, date, and body.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsRss()
    {
        var items = RssAtomReader.Read(Encoding.UTF8.GetBytes(Rss));
        await Assert.That(items.Length).IsEqualTo(2);
        await Assert.That(Encoding.UTF8.GetString(items[0].Title)).IsEqualTo("First Post");
        await Assert.That(Encoding.UTF8.GetString(items[0].Link)).IsEqualTo("https://blog.test/first");
        await Assert.That(Encoding.UTF8.GetString(items[0].ContentHtml)).IsEqualTo("Summary one");
    }

    /// <summary>The Atom reader prefers the <c>rel="alternate"</c> link and reads the content element.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsAtomPreferringAlternateLink()
    {
        var items = RssAtomReader.Read(Encoding.UTF8.GetBytes(Atom));
        await Assert.That(items.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(items[0].Link)).IsEqualTo("https://a.test/post");
        await Assert.That(Encoding.UTF8.GetString(items[0].ContentHtml)).IsEqualTo("<p>Body</p>");
    }

    /// <summary>Invalid XML throws a <see cref="ContentLoaderException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InvalidXmlThrows() =>
        await Assert.That(() => _ = RssAtomReader.Read(Encoding.UTF8.GetBytes("<not xml"))).Throws<ContentLoaderException>();

    /// <summary>The loader fetches the feed and produces one page per item with frontmatter and body.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoaderProducesPages()
    {
        var loader = new FeedContentLoader(
            (UrlPath)"https://blog.test/feed.xml",
            (PathSegment)"blog/external",
            () => new HttpClient(new StubHandler(Rss)),
            NullLogger.Instance);

        var pages = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None);

        await Assert.That(pages.Length).IsEqualTo(2);
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
        const string dupes = "<rss version=\"2.0\"><channel>" +
            "<item><title>Same Title</title><description>a</description></item>" +
            "<item><title>Same Title</title><description>b</description></item></channel></rss>";
        var loader = new FeedContentLoader((UrlPath)"https://x.test/f", (PathSegment)"p", () => new HttpClient(new StubHandler(dupes)), NullLogger.Instance);

        var pages = await loader.LoadAsync(new ContentLoaderContext(default), CancellationToken.None);

        await Assert.That(pages[0].RelativePath.Value).IsEqualTo("p/same-title.md");
        await Assert.That(pages[1].RelativePath.Value).IsEqualTo("p/same-title-2.md");
    }

    /// <summary>A canned-response handler returning a fixed body.</summary>
    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) });
        }
    }
}
