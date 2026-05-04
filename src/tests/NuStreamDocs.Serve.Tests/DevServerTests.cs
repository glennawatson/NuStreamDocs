// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using NuStreamDocs.Common;

namespace NuStreamDocs.Serve.Tests;

/// <summary>End-to-end HTTP tests against the dev server's request pipeline.</summary>
/// <remarks>
/// Each test stands up a real Kestrel server bound to an ephemeral loopback port, drives a
/// real <see cref="HttpClient"/> at it, and tears the host down on dispose. The dev server is
/// AOT-shaped so we can't use <c>WebApplicationFactory</c>'s in-memory transport — but a
/// loopback Kestrel is cheap enough that the per-test overhead is fine for a handful of
/// scenarios.
/// </remarks>
public class DevServerTests
{
    /// <summary>Requests for missing paths return the synthesised 404.html body with a 404 status.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NotFoundFallsBackTo404HtmlWithNotFoundStatus()
    {
        using var fixture = await DevServerFixture.StartAsync(SeedHomeAndNotFound);

        using var response = await fixture.Client.GetAsync(new Uri("/does-not-exist", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(body).Contains("Page not found");
    }

    /// <summary>Existing static files are served verbatim with a 200 status.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExistingFileServedWith200()
    {
        using var fixture = await DevServerFixture.StartAsync(SeedHomeOnly);

        using var response = await fixture.Client.GetAsync(new Uri("/index.html", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("Home");
    }

    /// <summary>The default-files middleware serves <c>index.html</c> for the directory root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryRootServesIndexHtml()
    {
        using var fixture = await DevServerFixture.StartAsync(SeedRootMarker);

        using var response = await fixture.Client.GetAsync(new Uri("/", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("ROOT_INDEX_MARKER");
    }

    /// <summary>With LiveReload disabled, HTML responses are not augmented with the reload script.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LiveReloadDisabledSkipsScriptInjection()
    {
        using var fixture = await DevServerFixture.StartAsync(SeedHelloBody, liveReload: false);

        using var response = await fixture.Client.GetAsync(new Uri("/index.html", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(body).DoesNotContain("__livereload");
    }

    /// <summary>With LiveReload enabled, HTML responses get the reload <c>&lt;script&gt;</c> spliced in before <c>&lt;/body&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LiveReloadInjectsScript()
    {
        using var fixture = await DevServerFixture.StartAsync(SeedHelloBody);

        using var response = await fixture.Client.GetAsync(new Uri("/index.html", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(body).Contains("__livereload");
    }

    /// <summary><see cref="DevServer.BuildUrl"/> renders <c>http://host:port</c> for both IP and DNS hosts.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlFormatsHostAndPort()
    {
        var url1 = DevServer.BuildUrl(new WatchAndServeOptions { Host = "127.0.0.1", Port = 9100 });
        var url2 = DevServer.BuildUrl(new WatchAndServeOptions { Host = "localhost", Port = 1234 });
        await Assert.That(url1.Value).IsEqualTo("http://127.0.0.1:9100");
        await Assert.That(url2.Value).IsEqualTo("http://localhost:1234");
    }

    /// <summary><see cref="DevServer.StartAsync"/> rejects null/empty roots and a null broker.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StartAsyncValidatesArguments()
    {
        await Assert.That(StartWithEmptyRoot).Throws<ArgumentException>();
        await Assert.That(StartWithNullBroker).Throws<ArgumentNullException>();
    }

    /// <summary>The public marker spans + the memory overload all surface the same bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PublicMarkersAreConsistent()
    {
        await Assert.That(DevServer.BodyCloseMarker.SequenceEqual("</body>"u8)).IsTrue();
        await Assert.That(DevServer.ReloadScriptMarker.Length).IsGreaterThan(0);
        await Assert.That(DevServer.ReloadScriptMemory.Span.SequenceEqual(DevServer.ReloadScriptMarker)).IsTrue();
    }

    /// <summary>Throws <see cref="ArgumentException"/> for the empty-output-root case in <see cref="StartAsyncValidatesArguments"/>.</summary>
    /// <returns>Async task.</returns>
    private static async Task StartWithEmptyRoot() =>
        await DevServer.StartAsync(string.Empty, default, new(), CancellationToken.None).ConfigureAwait(false);

    /// <summary>Throws <see cref="ArgumentNullException"/> for the null-broker case in <see cref="StartAsyncValidatesArguments"/>.</summary>
    /// <returns>Async task.</returns>
    private static async Task StartWithNullBroker() =>
        await DevServer.StartAsync("/tmp", default, null!, CancellationToken.None).ConfigureAwait(false);

    /// <summary>Seeds the temp root with an <c>index.html</c> and a <c>404.html</c>.</summary>
    /// <param name="dir">Output root.</param>
    private static void SeedHomeAndNotFound(string dir)
    {
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><title>Home</title>");
        File.WriteAllText(Path.Combine(dir, "404.html"), "<!doctype html><title>Missing</title><h1>Page not found</h1>");
    }

    /// <summary>Seeds the temp root with a single <c>index.html</c>.</summary>
    /// <param name="dir">Output root.</param>
    private static void SeedHomeOnly(string dir) =>
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><title>Home</title>");

    /// <summary>Seeds the temp root with a recognisable index marker.</summary>
    /// <param name="dir">Output root.</param>
    private static void SeedRootMarker(string dir) =>
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html>ROOT_INDEX_MARKER");

    /// <summary>Seeds the temp root with a body-bearing HTML page used by injection tests.</summary>
    /// <param name="dir">Output root.</param>
    private static void SeedHelloBody(string dir) =>
        File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><body>hello</body></html>");

    /// <summary>Disposable test fixture: starts a real Kestrel-backed dev server on an ephemeral port.</summary>
    private sealed class DevServerFixture : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="DevServerFixture"/> class.</summary>
        /// <param name="root">Temp directory used as the static root.</param>
        /// <param name="app">Started <see cref="Microsoft.AspNetCore.Builder.WebApplication"/>.</param>
        /// <param name="client">HTTP client wired to the chosen port.</param>
        private DevServerFixture(string root, Microsoft.AspNetCore.Builder.WebApplication app, HttpClient client)
        {
            Root = root;
            App = app;
            Client = client;
        }

        /// <summary>Gets the temp output root.</summary>
        public string Root { get; }

        /// <summary>Gets the underlying web application.</summary>
        public Microsoft.AspNetCore.Builder.WebApplication App { get; }

        /// <summary>Gets the HTTP client wired to the dev server's base address.</summary>
        public HttpClient Client { get; }

        /// <summary>Starts the server, populates the static root via <paramref name="seed"/>, and yields a fixture.</summary>
        /// <param name="seed">Callback invoked with the absolute root path to populate fixture content.</param>
        /// <param name="liveReload">When true (default) the live-reload middleware is wired in.</param>
        /// <returns>The fixture.</returns>
        public static async Task<DevServerFixture> StartAsync(Action<string> seed, bool liveReload = true)
        {
            var root = Path.Combine(Path.GetTempPath(), "smkd-devserver-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(root);
            seed(root);

            var port = ReserveLoopbackPort();
            var options = new WatchAndServeOptions
            {
                Host = "127.0.0.1",
                Port = port,
                LiveReload = liveReload,
            };

            var app = await DevServer.StartAsync(root, options, new(), CancellationToken.None).ConfigureAwait(false);
            var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute) };
            return new(root, app, client);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Client.Dispose();
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                App.StopAsync(stopCts.Token).GetAwaiter().GetResult();
            }
            catch
            {
                // best-effort shutdown
            }

            try
            {
                ((IDisposable)App).Dispose();
            }
            catch
            {
                // best-effort dispose
            }

            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }

        /// <summary>Asks the OS for a free TCP loopback port and immediately releases it; the dev server then binds to it.</summary>
        /// <returns>Free port.</returns>
        private static int ReserveLoopbackPort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
