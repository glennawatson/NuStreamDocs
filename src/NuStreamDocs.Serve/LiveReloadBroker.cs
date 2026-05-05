// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace NuStreamDocs.Serve;

/// <summary>
/// Connection registry for the LiveReload websocket. The HTTP middleware
/// registers each connecting browser; the watch loop calls
/// <see cref="ReloadAllAsync"/> after a successful rebuild.
/// </summary>
internal sealed class LiveReloadBroker
{
    /// <summary>The wire payload sent to connected browsers on rebuild.</summary>
    private static readonly byte[] ReloadPayload = [.. "reload"u8];

    /// <summary>Tracks every currently-connected browser. Concurrent because clients connect/disconnect from request-handler threads.</summary>
    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();

    /// <summary>Gets the current connected-client count.</summary>
    public int ConnectedCount => _clients.Count;

    /// <summary>Registers <paramref name="socket"/> until the browser disconnects.</summary>
    /// <param name="socket">Newly-accepted client websocket.</param>
    /// <param name="cancellationToken">Cancellation token tied to the request lifetime.</param>
    /// <returns>Task that completes when the client disconnects.</returns>
    public async Task TrackAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);
        var id = Guid.NewGuid();
        _clients[id] = socket;
        try
        {
            // Block until the client disconnects; we never receive messages from it.
            var buffer = new byte[16];
            while (socket.State is WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType is WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (WebSocketException)
        {
            // client gone; drop silently
        }
        finally
        {
            _clients.TryRemove(id, out _);
        }
    }

    /// <summary>Sends a reload signal to every connected browser; returns the count that received it.</summary>
    /// <returns>Count of clients that successfully received the message.</returns>
    public async Task<int> ReloadAllAsync()
    {
        if (_clients.IsEmpty)
        {
            return 0;
        }

        var sent = 0;
        foreach (var kvp in _clients)
        {
            if (await TrySendReloadAsync(kvp.Value).ConfigureAwait(false))
            {
                sent++;
                continue;
            }

            _clients.TryRemove(kvp.Key, out _);
        }

        return sent;
    }

    /// <summary>Aborts every tracked WebSocket so the request handlers exit promptly during shutdown.</summary>
    /// <remarks>
    /// Browsers don't always reply to the close handshake — relying on a graceful close would let
    /// the dev server hang on Ctrl+C waiting forever. <see cref="WebSocket.Abort"/> tears the
    /// connection down without waiting for the peer, then the registry clears.
    /// </remarks>
    public void AbortAll()
    {
        foreach (var kvp in _clients)
        {
            try
            {
                kvp.Value.Abort();
            }
            catch (ObjectDisposedException)
            {
                // already torn down
            }
        }

        _clients.Clear();
    }

    /// <summary>Sends the reload payload to a single client; swallows transport errors.</summary>
    /// <param name="socket">Target websocket.</param>
    /// <returns>True when the send succeeded.</returns>
    private static async Task<bool> TrySendReloadAsync(WebSocket socket)
    {
        if (socket.State is not WebSocketState.Open)
        {
            return false;
        }

        try
        {
            await socket.SendAsync(ReloadPayload, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (WebSocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
