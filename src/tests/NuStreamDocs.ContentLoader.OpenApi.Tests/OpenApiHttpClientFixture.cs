// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.ContentLoader.OpenApi.Tests;

/// <summary>Owns an HTTP client whose responses carry an OpenAPI fixture.</summary>
internal sealed class OpenApiHttpClientFixture : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="OpenApiHttpClientFixture"/> class.</summary>
    /// <param name="status">Status returned by each request.</param>
    /// <param name="body">Response body.</param>
    internal OpenApiHttpClientFixture(HttpStatusCode status, string body) => Client = new(new ResponseHandler(status, body));

    /// <summary>Gets the client owned by the test.</summary>
    internal HttpClient Client { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Client.Dispose();

    /// <summary>Returns the configured HTTP response without network access.</summary>
    /// <param name="status">Response status.</param>
    /// <param name="body">Response body.</param>
    private sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) });
    }
}
