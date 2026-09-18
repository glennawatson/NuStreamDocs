// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.ContentLoader.Feed.Tests;

/// <summary>Owns the reusable HTTP client supplied to a feed loader test.</summary>
internal sealed class FeedHttpClientFixture : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="FeedHttpClientFixture"/> class.</summary>
    /// <param name="body">Feed XML response body.</param>
    /// <param name="status">Status returned for every request.</param>
    internal FeedHttpClientFixture(byte[] body, HttpStatusCode status) => Client = new(new StubHandler(body, status));

    /// <summary>Gets the client owned by this fixture.</summary>
    internal HttpClient Client { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Client.Dispose();

    /// <summary>Returns a fixed feed response.</summary>
    /// <param name="body">Response body.</param>
    /// <param name="status">Response status.</param>
    private sealed class StubHandler(byte[] body, HttpStatusCode status) : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }
}
