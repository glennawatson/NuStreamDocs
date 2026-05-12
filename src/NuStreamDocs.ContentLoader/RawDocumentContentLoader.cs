// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.ContentLoader.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.ContentLoader;

/// <summary>
/// Pulls a fixed list of raw Markdown documents over HTTP — each response body is used verbatim as a
/// page. Works for any host that serves raw files (GitHub, GitLab, Gitea, a CDN, …); the document may
/// already carry its own frontmatter or not.
/// </summary>
public sealed class RawDocumentContentLoader : IContentLoader
{
    /// <summary>The documents to fetch.</summary>
    private readonly RawDocumentEntry[] _entries;

    /// <summary>Extra request headers (name/value UTF-8 byte pairs) sent with every fetch.</summary>
    private readonly (byte[] Name, byte[] Value)[] _headers;

    /// <summary>HTTP client factory; null means the loader owns a short-lived client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="RawDocumentContentLoader"/> class.</summary>
    /// <param name="entries">The documents to fetch.</param>
    public RawDocumentContentLoader(RawDocumentEntry[] entries)
        : this(entries, [], httpClientFactory: null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RawDocumentContentLoader"/> class.</summary>
    /// <param name="entries">The documents to fetch.</param>
    /// <param name="headers">Extra request headers (UTF-8 name/value byte pairs).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public RawDocumentContentLoader(RawDocumentEntry[] entries, (byte[] Name, byte[] Value)[] headers, ILogger logger)
        : this(entries, headers, httpClientFactory: null, logger)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RawDocumentContentLoader"/> class.</summary>
    /// <param name="entries">The documents to fetch.</param>
    /// <param name="headers">Extra request headers (UTF-8 name/value byte pairs).</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client; null means the loader owns a short-lived client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public RawDocumentContentLoader(RawDocumentEntry[] entries, (byte[] Name, byte[] Value)[] headers, Func<HttpClient>? httpClientFactory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(headers);
        _entries = entries;
        _headers = headers;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "raw-document"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        _ = context;
        if (_entries is [])
        {
            return [];
        }

        if (_httpClientFactory is not null)
        {
            return await FetchAllAsync(_httpClientFactory(), cancellationToken).ConfigureAwait(false);
        }

        using HttpClient owned = new();
        return await FetchAllAsync(owned, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches every configured document and builds the page array.</summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One page per document.</returns>
    private async Task<SyntheticPage[]> FetchAllAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var pages = new SyntheticPage[_entries.Length];
        for (var i = 0; i < _entries.Length; i++)
        {
            var entry = _entries[i];
            var bytes = await FetchAsync(client, entry.Url, cancellationToken).ConfigureAwait(false);
            pages[i] = new(entry.RoutePath, bytes);
        }

        return pages;
    }

    /// <summary>Fetches one document.</summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="url">Document URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The UTF-8 document bytes.</returns>
    private async Task<byte[]> FetchAsync(HttpClient client, UrlPath url, CancellationToken cancellationToken)
    {
        Uri endpoint = new(url.Value, UriKind.Absolute);
        using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
        for (var i = 0; i < _headers.Length; i++)
        {
            request.Headers.TryAddWithoutValidation(Encoding.UTF8.GetString(_headers[i].Name), Encoding.UTF8.GetString(_headers[i].Value));
        }

        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ContentLoaderLoggingHelper.LogFetchFailed(_logger, "raw-document", url.Value, ex.Message);
            throw new ContentLoaderException(StringCompose.Concat("HTTP request to ", url.Value, " failed: ", ex.Message), ex);
        }
    }
}
