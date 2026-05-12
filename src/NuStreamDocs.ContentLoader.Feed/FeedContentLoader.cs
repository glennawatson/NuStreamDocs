// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.ContentLoader.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader.Feed;

/// <summary>
/// Pulls an external RSS or Atom feed and turns each item into a page under a route prefix —
/// the consume-side counterpart to NuStreamDocs.Feed. Each page carries <c>title</c>, <c>date</c>,
/// <c>source</c>, and <c>external_url</c> frontmatter; the body is the item's content (usually HTML).
/// </summary>
public sealed class FeedContentLoader : IContentLoader
{
    /// <summary>The feed URL.</summary>
    private readonly UrlPath _feedUrl;

    /// <summary>Subdirectory the synthesized pages are placed under (e.g. <c>blog/external</c>).</summary>
    private readonly PathSegment _routePrefix;

    /// <summary>HTTP client factory; null means the loader owns a short-lived client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="FeedContentLoader"/> class.</summary>
    /// <param name="feedUrl">The feed URL.</param>
    /// <param name="routePrefix">Subdirectory the synthesized pages are placed under.</param>
    public FeedContentLoader(UrlPath feedUrl, PathSegment routePrefix)
        : this(feedUrl, routePrefix, httpClientFactory: null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FeedContentLoader"/> class.</summary>
    /// <param name="feedUrl">The feed URL.</param>
    /// <param name="routePrefix">Subdirectory the synthesized pages are placed under.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public FeedContentLoader(UrlPath feedUrl, PathSegment routePrefix, ILogger logger)
        : this(feedUrl, routePrefix, httpClientFactory: null, logger)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FeedContentLoader"/> class.</summary>
    /// <param name="feedUrl">The feed URL.</param>
    /// <param name="routePrefix">Subdirectory the synthesized pages are placed under.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client; null means the loader owns a short-lived client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public FeedContentLoader(UrlPath feedUrl, PathSegment routePrefix, Func<HttpClient>? httpClientFactory, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(feedUrl.Value);
        ArgumentException.ThrowIfNullOrEmpty(routePrefix.Value);
        _feedUrl = feedUrl;
        _routePrefix = routePrefix;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "feed"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        var xml = await FetchAsync(cancellationToken).ConfigureAwait(false);
        var items = RssAtomReader.Read(xml);
        return BuildPages(items);
    }

    /// <summary>Fetches the feed XML.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTF-8 feed bytes.</returns>
    private async Task<byte[]> FetchAsync(CancellationToken cancellationToken)
    {
        if (_httpClientFactory is not null)
        {
            return await GetBytesAsync(_httpClientFactory(), cancellationToken).ConfigureAwait(false);
        }

        using HttpClient owned = new();
        return await GetBytesAsync(owned, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Issues the GET and reads the body.</summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTF-8 response body.</returns>
    private async Task<byte[]> GetBytesAsync(HttpClient client, CancellationToken cancellationToken)
    {
        Uri endpoint = new(_feedUrl.Value, UriKind.Absolute);
        try
        {
            using var response = await client.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ContentLoaderLoggingHelper.LogFetchFailed(_logger, "feed", _feedUrl.Value, ex.Message);
            throw new ContentLoaderException(StringCompose.Concat("Failed to fetch feed ", _feedUrl.Value, ": ", ex.Message), ex);
        }
    }

    /// <summary>Builds one page per feed item, with collision-free routes.</summary>
    /// <param name="items">The parsed feed items.</param>
    /// <returns>The synthesized pages.</returns>
    private SyntheticPage[] BuildPages(FeedItem[] items)
    {
        var pages = new SyntheticPage[items.Length];
        HashSet<byte[]> usedRoutes = new(ByteArrayComparer.Instance);
        var feedUrlBytes = Encoding.UTF8.GetBytes(_feedUrl.Value);
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var slugSource = item.Title is [_, ..] ? item.Title : item.Identifier;
            var route = UniqueRoute(Slug.FromBytes(slugSource), usedRoutes);
            pages[i] = new(new FilePath(Encoding.UTF8.GetString(route)), FeedMarkdown.Build(item, feedUrlBytes));
        }

        return pages;
    }

    /// <summary>Composes <c>{prefix}/{slug}.md</c>, appending a numeric suffix to the slug until the route is unused.</summary>
    /// <param name="slug">Base slug bytes.</param>
    /// <param name="usedRoutes">Routes already taken in this run.</param>
    /// <returns>A unique route's bytes.</returns>
    private byte[] UniqueRoute(byte[] slug, HashSet<byte[]> usedRoutes)
    {
        Span<byte> digits = stackalloc byte[16];
        var suffix = 1;
        while (true)
        {
            ArrayBufferWriter<byte> writer = new(_routePrefix.Value.Length + slug.Length + 8);
            Encoding.UTF8.GetBytes(_routePrefix.Value.AsSpan(), writer);
            writer.Write("/"u8);
            writer.Write(slug);
            if (suffix > 1)
            {
                writer.Write("-"u8);
                suffix.TryFormat(digits, out var written);
                writer.Write(digits[..written]);
            }

            writer.Write(".md"u8);
            var route = writer.WrittenSpan.ToArray();
            if (usedRoutes.Add(route))
            {
                return route;
            }

            suffix++;
        }
    }
}
