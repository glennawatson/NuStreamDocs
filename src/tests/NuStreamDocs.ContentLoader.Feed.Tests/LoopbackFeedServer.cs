// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NuStreamDocs.ContentLoader.Feed.Tests;

/// <summary>Serves feed responses over persistent connections and observes request cookies.</summary>
internal sealed class LoopbackFeedServer : IAsyncDisposable
{
    /// <summary>Bounds incomplete requests when a test fails.</summary>
    private const int RequestTimeoutSeconds = 10;

    /// <summary>Capacity for the small request headers sent by the fixture.</summary>
    private const int HeaderBufferSize = 1024;

    /// <summary>Listener reserved for this fixture.</summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    /// <summary>Cancels pending network operations during teardown.</summary>
    private readonly CancellationTokenSource _shutdown = new(TimeSpan.FromSeconds(RequestTimeoutSeconds));

    /// <summary>Initializes a new instance of the <see cref="LoopbackFeedServer"/> class.</summary>
    /// <param name="requestCount">Number of feed requests to serve.</param>
    /// <param name="body">Feed XML returned by every request.</param>
    internal LoopbackFeedServer(int requestCount, byte[] body)
    {
        _listener.Start();
        Endpoint = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/feed.xml";
        Requests = ServeAsync(_listener, requestCount, body, _shutdown.Token);
    }

    /// <summary>Gets the feed endpoint served by this fixture.</summary>
    internal string Endpoint { get; }

    /// <summary>Gets the connection count and cookie presence for each request.</summary>
    internal Task<(int Connections, bool[] Cookies)> Requests { get; }

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
            // Teardown can interrupt a request when an assertion fails.
        }
        finally
        {
            _shutdown.Dispose();
        }
    }

    /// <summary>Responds to requests while recording transport reuse and cookies.</summary>
    /// <param name="listener">Started loopback listener.</param>
    /// <param name="requestCount">Number of requests to serve.</param>
    /// <param name="body">Feed XML bytes.</param>
    /// <param name="cancellationToken">Token that bounds network operations.</param>
    /// <returns>Connection count and cookie presence for each request.</returns>
    private static async Task<(int Connections, bool[] Cookies)> ServeAsync(TcpListener listener, int requestCount, byte[] body, CancellationToken cancellationToken)
    {
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: keep-alive\r\nSet-Cookie: feed=session; Path=/\r\n\r\n");
        var cookies = new bool[requestCount];
        var connections = 0;
        var requestIndex = 0;
        while (requestIndex < requestCount)
        {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken);
            connections++;
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, HeaderBufferSize, true);
            while (requestIndex < requestCount && await reader.ReadLineAsync(cancellationToken) is not null)
            {
                while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
                {
                    cookies[requestIndex] |= line.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase);
                }

                requestIndex++;
                await stream.WriteAsync(headers, cancellationToken);
                await stream.WriteAsync(body, cancellationToken);
            }
        }

        return (connections, cookies);
    }
}
