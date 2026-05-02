// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace NuStreamDocs.Serve;

/// <summary>
/// Wraps a slim AOT-compatible <see cref="WebApplication"/>: serves the
/// output directory as static files, exposes a <c>/__livereload</c>
/// websocket endpoint via middleware-style routing (no minimal-API
/// reflection binding), and injects a tiny <c>&lt;script&gt;</c> tag
/// into HTML responses so every page in the served output participates
/// in live-reload without hand-editing.
/// </summary>
internal static class DevServer
{
    /// <summary>Path of the websocket endpoint browsers connect to.</summary>
    private const string LiveReloadPath = "/__livereload";

    /// <summary>
    /// Injected JavaScript snippet used to enable live-reload functionality in served HTML pages.
    /// This script establishes a WebSocket connection to the server at the live-reload endpoint
    /// and listens for messages instructing the browser to reload the page.
    /// </summary>
    private const string ReloadScript =
        "<script>(function(){"
        + "var s=new WebSocket((location.protocol==='https:'?'wss':'ws')+'://'+location.host+'" + LiveReloadPath + "');"
        + "s.onmessage=function(e){if(e.data==='reload'){location.reload()}};"
        + "s.onerror=function(){};"
        + "})();</script>";

    /// <summary>UTF-8 bytes for the closing body tag we splice the script before.</summary>
    private static readonly byte[] BodyClose = "</body>"u8.ToArray();

    /// <summary>UTF-8 bytes for the reload script.</summary>
    private static readonly byte[] ReloadScriptBytes = Encoding.UTF8.GetBytes(ReloadScript);

    /// <summary>Gets the closing-body marker the injection middleware splits HTML on.</summary>
    public static ReadOnlySpan<byte> BodyCloseMarker => BodyClose;

    /// <summary>Gets the reload-script bytes the injection middleware splices into HTML responses.</summary>
    public static ReadOnlySpan<byte> ReloadScriptMarker => ReloadScriptBytes;

    /// <summary>Gets the reload-script bytes as a <see cref="ReadOnlyMemory{T}"/>.</summary>
    /// <remarks>
    /// Async writers hand it to
    /// <see cref="System.IO.Stream.WriteAsync(System.ReadOnlyMemory{byte}, System.Threading.CancellationToken)"/>
    /// without a per-call <c>ToArray</c> copy.
    /// </remarks>
    public static ReadOnlyMemory<byte> ReloadScriptMemory => ReloadScriptBytes;

    /// <summary>Builds and starts the AOT-friendly <see cref="WebApplication"/> bound to <paramref name="options"/>.</summary>
    /// <param name="outputRoot">Directory to serve as static content.</param>
    /// <param name="options">Watch + serve options.</param>
    /// <param name="broker">LiveReload connection registry.</param>
    /// <param name="cancellationToken">Cancellation token; cancellation triggers a graceful shutdown.</param>
    /// <returns>Started <see cref="WebApplication"/>.</returns>
    public static async Task<WebApplication> StartAsync(string outputRoot, WatchAndServeOptions options, LiveReloadBroker broker, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot);
        ArgumentNullException.ThrowIfNull(broker);

        Directory.CreateDirectory(outputRoot);
        var app = BuildApplication(outputRoot, options, broker);
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return app;
    }

    /// <summary>Builds the bind URL from <paramref name="options"/>.</summary>
    /// <param name="options">Options.</param>
    /// <returns>URL string suitable for <see cref="HostingAbstractionsWebHostBuilderExtensions.UseUrls"/>.</returns>
    [SuppressMessage(
        "Justification",
        "S5332: Using http protocol is insecure. Use https instead.",
        Justification = "Local dev only.")]
    public static string BuildUrl(in WatchAndServeOptions options) =>
        string.Create(CultureInfo.InvariantCulture, $"http://{options.Host}:{options.Port}");

    /// <summary>Builds the slim WebApplication without starting it.</summary>
    /// <param name="outputRoot">Static-file root.</param>
    /// <param name="options">Watch + serve options.</param>
    /// <param name="broker">LiveReload registry.</param>
    /// <returns>Configured <see cref="WebApplication"/>.</returns>
    private static WebApplication BuildApplication(string outputRoot, WatchAndServeOptions options, LiveReloadBroker broker)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel((_, kestrel) =>
        {
            if (IPAddress.TryParse(options.Host, out var ip))
            {
                kestrel.Listen(ip, options.Port);
                return;
            }

            kestrel.ListenLocalhost(options.Port);
        });
        builder.Services.AddSingleton(broker);
        var app = builder.Build();
        ConfigurePipeline(app, outputRoot, options);
        return app;
    }

    /// <summary>Wires the request pipeline: websocket endpoint, optional script injection, static files.</summary>
    /// <param name="app">Application to configure.</param>
    /// <param name="outputRoot">Static root.</param>
    /// <param name="options">Watch + serve options.</param>
    private static void ConfigurePipeline(WebApplication app, string outputRoot, in WatchAndServeOptions options)
    {
        app.UseWebSockets();

        // Path-restricted MapGet avoids the manual path-check in Use().
        // When using a specialized RequestDelegate, RDG emits no reflection.
        app.MapGet(LiveReloadPath, LiveReloadDispatchAsync);

        if (options.LiveReload)
        {
            app.Use(HtmlInjectionMiddleware.InvokeAsync);
        }

        var fileProvider = new PhysicalFileProvider(Path.GetFullPath(outputRoot));
        var defaultFiles = new DefaultFilesOptions { FileProvider = fileProvider };
        defaultFiles.DefaultFileNames.Clear();
        defaultFiles.DefaultFileNames.Add("index.html");
        app.UseDefaultFiles(defaultFiles);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = new FileExtensionContentTypeProvider(),
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });
    }

    /// <summary>Routes <c>/__livereload</c> to the websocket handler; everything else falls through to static-file middleware.</summary>
    /// <param name="ctx">HTTP context.</param>
    /// <returns>Async task.</returns>
    private static async Task LiveReloadDispatchAsync(HttpContext ctx)
    {
        if (ctx.WebSockets.IsWebSocketRequest)
        {
            var broker = ctx.RequestServices.GetRequiredService<LiveReloadBroker>();
            var socket = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            await broker.TrackAsync(socket, ctx.RequestAborted).ConfigureAwait(false);
            if (socket.State is WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None).ConfigureAwait(false);
            }

            return;
        }

        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
}
