// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NuStreamDocs.ContentLoader.Tests;

/// <summary>Serves a bounded sequence of local responses and captures request headers.</summary>
internal sealed class LoopbackHttpServer : IAsyncDisposable
{
    /// <summary>Bounds an incomplete request so a failing test cannot leave the server waiting.</summary>
    private const int RequestTimeoutSeconds = 10;

    /// <summary>Accommodates the small headers used by transport tests.</summary>
    private const int HeaderBufferSize = 1024;

    /// <summary>The listener reserved for this test.</summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    /// <summary>Cancels pending accepts and reads when the test ends.</summary>
    private readonly CancellationTokenSource _shutdown = new(TimeSpan.FromSeconds(RequestTimeoutSeconds));

    /// <summary>Initializes a new instance of the <see cref="LoopbackHttpServer"/> class.</summary>
    /// <param name="requestCount">Number of requests to serve.</param>
    /// <param name="responseBody">Body returned by each request.</param>
    internal LoopbackHttpServer(int requestCount, string responseBody)
    {
        _listener.Start();
        Endpoint = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/content";
        Requests = ServeAsync(_listener, requestCount, Encoding.UTF8.GetBytes(responseBody), _shutdown.Token);
    }

    /// <summary>Gets the local endpoint served by the fixture.</summary>
    internal string Endpoint { get; }

    /// <summary>Gets the headers received by each request, in arrival order.</summary>
    internal Task<Dictionary<string, string>[]> Requests { get; }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        _listener.Dispose();
        try
        {
            _ = await Requests;
        }
        catch (OperationCanceledException)
        {
            // A test may stop before all expected requests arrive.
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    /// <summary>Returns one captured header set for each served request.</summary>
    /// <param name="listener">Started loopback listener.</param>
    /// <param name="requestCount">Number of requests to serve.</param>
    /// <param name="body">Response bytes.</param>
    /// <param name="cancellationToken">Token that bounds request processing.</param>
    /// <returns>Request headers in arrival order.</returns>
    private static async Task<Dictionary<string, string>[]> ServeAsync(
        TcpListener listener,
        int requestCount,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\nSet-Cookie: loader=session; Path=/\r\n\r\n");
        var requests = new Dictionary<string, string>[requestCount];
        for (var i = 0; i < requests.Length; i++)
        {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, HeaderBufferSize, true);
            _ = await reader.ReadLineAsync(cancellationToken);
            var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);
                requestHeaders.Add(line[..separator], line[(separator + 1)..].Trim());
            }

            requests[i] = requestHeaders;
            await stream.WriteAsync(headers, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
        }

        return requests;
    }
}
