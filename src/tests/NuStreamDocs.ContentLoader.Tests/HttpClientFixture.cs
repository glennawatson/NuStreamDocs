// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.ContentLoader.Tests;

/// <summary>Owns a reusable client and its request-recording transport for one test.</summary>
internal sealed class HttpClientFixture : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="HttpClientFixture"/> class.</summary>
    /// <param name="status">Status returned by every request.</param>
    /// <param name="body">Body returned by every request.</param>
    internal HttpClientFixture(HttpStatusCode status, string body)
    {
        Handler = new(_ => (status, body));
        Client = new(Handler);
    }

    /// <summary>Gets the client whose lifetime is owned by this fixture.</summary>
    internal HttpClient Client { get; }

    /// <summary>Gets the transport that records requests made by the client.</summary>
    internal StubHttpHandler Handler { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Client.Dispose();
}
