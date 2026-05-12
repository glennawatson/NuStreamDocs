// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Text;

namespace NuStreamDocs.ContentLoader.Tests;

/// <summary>A canned-response <see cref="HttpMessageHandler"/> for loader tests.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    /// <summary>Maps each request to the status code and body to return.</summary>
    private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> _respond;

    /// <summary>Initializes a new instance of the <see cref="StubHttpHandler"/> class.</summary>
    /// <param name="respond">Maps a request to a status code and response body.</param>
    public StubHttpHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> respond) => _respond = respond;

    /// <summary>Gets the requests this handler has seen.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Builds an <see cref="HttpClient"/> that always returns <paramref name="body"/> with HTTP 200.</summary>
    /// <param name="body">Response body.</param>
    /// <returns>The configured client.</returns>
    public static HttpClient ClientReturning(string body) => new(new StubHttpHandler(_ => (HttpStatusCode.OK, body)));

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var (status, body) = _respond(request);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) });
    }
}
