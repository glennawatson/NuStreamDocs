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
/// Fetches a JSON document from an HTTP endpoint — a REST API, or a GraphQL endpoint when a request
/// body is supplied — and turns the array it locates into Markdown pages via a <see cref="ContentMapping"/>.
/// </summary>
public sealed class HttpContentLoader : IContentLoader
{
    /// <summary>The endpoint URL.</summary>
    private readonly UrlPath _url;

    /// <summary>JSON request body for a POST (e.g. a GraphQL query); empty issues a GET.</summary>
    private readonly byte[] _requestBody;

    /// <summary>Extra request headers (name/value UTF-8 byte pairs).</summary>
    private readonly (byte[] Name, byte[] Value)[] _headers;

    /// <summary>Field mapping.</summary>
    private readonly ContentMapping _mapping;

    /// <summary>HTTP client factory; null means the loader owns a short-lived client.</summary>
    private readonly Func<HttpClient>? _httpClientFactory;

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="HttpContentLoader"/> class issuing a GET.</summary>
    /// <param name="url">Endpoint URL.</param>
    /// <param name="mapping">Field mapping.</param>
    public HttpContentLoader(UrlPath url, ContentMapping mapping)
        : this(url, [], [], mapping, null, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HttpContentLoader"/> class.</summary>
    /// <param name="url">Endpoint URL.</param>
    /// <param name="requestBody">JSON request body for a POST (e.g. a GraphQL query); empty issues a GET.</param>
    /// <param name="headers">Extra request headers (UTF-8 name/value byte pairs).</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public HttpContentLoader(
        UrlPath url,
        byte[] requestBody,
        (byte[] Name, byte[] Value)[] headers,
        ContentMapping mapping,
        ILogger logger)
        : this(url, requestBody, headers, mapping, null, logger)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HttpContentLoader"/> class.</summary>
    /// <param name="url">Endpoint URL.</param>
    /// <param name="requestBody">JSON request body for a POST (e.g. a GraphQL query); empty issues a GET.</param>
    /// <param name="headers">Extra request headers (UTF-8 name/value byte pairs).</param>
    /// <param name="mapping">Field mapping.</param>
    /// <param name="httpClientFactory">Factory producing the HTTP client; null means the loader owns a short-lived client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public HttpContentLoader(
        UrlPath url,
        byte[] requestBody,
        (byte[] Name, byte[] Value)[] headers,
        ContentMapping mapping,
        Func<HttpClient>? httpClientFactory,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(url.Value);
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(mapping);
        mapping.Validate();
        _url = url;
        _requestBody = requestBody;
        _headers = headers;
        _mapping = mapping;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "http"u8;

    /// <inheritdoc/>
    public async ValueTask<SyntheticPage[]> LoadAsync(ContentLoaderContext context, CancellationToken cancellationToken)
    {
        _ = context;

        if (_httpClientFactory is not null)
        {
            var json = await FetchAsync(_httpClientFactory(), cancellationToken).ConfigureAwait(false);
            return JsonContentMapper.Map(json, _mapping, Name, _logger);
        }

        using HttpClient owned = new();
        var fetched = await FetchAsync(owned, cancellationToken).ConfigureAwait(false);
        return JsonContentMapper.Map(fetched, _mapping, Name, _logger);
    }

    /// <summary>Issues the request and returns the response body bytes.</summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The UTF-8 response body.</returns>
    private async Task<byte[]> FetchAsync(HttpClient client, CancellationToken cancellationToken)
    {
        Uri endpoint = new(_url.Value, UriKind.Absolute);
        using HttpRequestMessage request = new(_requestBody is [_, ..] ? HttpMethod.Post : HttpMethod.Get, endpoint);
        if (_requestBody is [_, ..])
        {
            request.Content = new ByteArrayContent(_requestBody);
            request.Content.Headers.ContentType = new("application/json");
        }

        ApplyHeaders(request.Headers);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ContentLoaderLoggingHelper.LogFetchFailed(_logger, "http", _url.Value, ex.Message);
            throw new ContentLoaderException(
                StringCompose.Concat("HTTP request to ", _url.Value, " failed: ", ex.Message),
                ex);
        }
    }

    /// <summary>Copies the configured extra headers onto the request.</summary>
    /// <param name="headers">Request headers to populate.</param>
    private void ApplyHeaders(HttpRequestHeaders headers)
    {
        for (var i = 0; i < _headers.Length; i++)
        {
            headers.TryAddWithoutValidation(
                Encoding.UTF8.GetString(_headers[i].Name),
                Encoding.UTF8.GetString(_headers[i].Value));
        }
    }
}
