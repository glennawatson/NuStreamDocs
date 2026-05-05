// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>End-to-end tests that drive the plugin through the real <c>DocBuilder</c> pipeline against a loopback HTTP server.</summary>
public class PrivacyPluginLifecycleTests
{
    /// <summary>A markdown page with an absolute <c>img</c> URL ends up referencing the local copy and the asset bytes are present on disk.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LocalizesExternalImageOnBuild()
    {
        using var fixture = TempSite.Create();
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            const string AssetText = "fake-png-bytes";
            server.AddRoute("/logo.png", "image/png", Encoding.UTF8.GetBytes(AssetText));
            server.Start();

            var pageUrl = $"{server.BaseUrl}logo.png";
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "page.md"), $"![logo]({pageUrl})\n");

            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .UsePrivacy(static opts => opts with { DownloadParallelism = 2 })
                .BuildAsync();

            var rendered = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "page.html"));
            await Assert.That(rendered).Contains("/assets/external/");
            await Assert.That(rendered).DoesNotContain(pageUrl);

            var localized = Directory.GetFiles(Path.Combine(fixture.Output, "assets", "external"));
            await Assert.That(localized).HasSingleItem();
            var bytes = await File.ReadAllBytesAsync(localized[0]);
            await Assert.That(Encoding.UTF8.GetString(bytes)).IsEqualTo(AssetText);
        }
    }

    /// <summary>The same external URL shared across two pages downloads exactly once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DedupesAcrossPages()
    {
        using var fixture = TempSite.Create();
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            server.AddRoute("/shared.png", "image/png", [.. "shared"u8]);
            server.Start();

            var url = $"{server.BaseUrl}shared.png";
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "a.md"), $"![]({url})");
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "b.md"), $"![]({url})");

            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .UsePrivacy()
                .BuildAsync();

            await Assert.That(server.HitCountFor("/shared.png")).IsEqualTo(1);
        }
    }

    /// <summary>A CSS file referencing a font URL drives a second download pass; the font lands locally too.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FollowsNestedCssUrls()
    {
        using var fixture = TempSite.Create();
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            const string FontBytes = "FONT-DATA";
            server.Start();
            var fontUrl = $"{server.BaseUrl}font.woff2";
            server.AddRoute("/font.woff2", "font/woff2", Encoding.UTF8.GetBytes(FontBytes));
            server.AddRoute("/fonts.css", "text/css", Encoding.UTF8.GetBytes($"@font-face {{ src: url({fontUrl}) }}"));

            var pageUrl = $"{server.BaseUrl}fonts.css";
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "page.md"), $"![]({pageUrl})\n");

            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .UsePrivacy()
                .BuildAsync();

            var localized = Directory.GetFiles(Path.Combine(fixture.Output, "assets", "external"));
            await Assert.That(localized.Length).IsEqualTo(2);

            var fontFile = localized.First(static f => f.EndsWith(".woff2", StringComparison.Ordinal));
            await Assert.That(await File.ReadAllTextAsync(fontFile)).IsEqualTo(FontBytes);

            var cssFile = localized.First(static f => f.EndsWith(".css", StringComparison.Ordinal));
            var rewrittenCss = await File.ReadAllTextAsync(cssFile);
            await Assert.That(rewrittenCss).Contains("/assets/external/");
            await Assert.That(rewrittenCss).DoesNotContain(fontUrl);
        }
    }

    /// <summary>Audit-only mode writes a manifest of detected external URLs and never hits the network.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AuditOnlyModeEmitsManifestWithoutDownloading()
    {
        using var fixture = TempSite.Create();
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            server.AddRoute("/asset.png", "image/png", [.. "should-not-be-fetched"u8]);
            server.Start();

            var url = $"{server.BaseUrl}asset.png";
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "page.md"), $"![]({url})\n");

            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .UsePrivacy(static opts => opts with { AuditOnly = true })
                .BuildAsync();

            await Assert.That(server.HitCountFor("/asset.png")).IsEqualTo(0);
            await Assert.That(Directory.Exists(Path.Combine(fixture.Output, "assets", "external"))).IsFalse();

            var manifestPath = Path.Combine(fixture.Output, "privacy-audit.json");
            await Assert.That(File.Exists(manifestPath)).IsTrue();
            var manifest = await File.ReadAllTextAsync(manifestPath);
            await Assert.That(manifest).Contains(url);
            await Assert.That(manifest).Contains("\"auditOnly\": true");
        }
    }

    /// <summary><c>PrivacyOptions.FailOnError</c> turns a 404 into a thrown <c>PrivacyDownloadException</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FailOnErrorRaisesOnUpstreamFailure()
    {
        using var fixture = TempSite.Create();
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            server.Start();
            var url = $"{server.BaseUrl}missing.png";
            var inputRoot = fixture.Root;
            var outputRoot = fixture.Output;
            await File.WriteAllTextAsync(Path.Combine(inputRoot, "page.md"), $"![]({url})");

            await Assert.That((Func<Task>)Build).Throws<PrivacyDownloadException>();

            Task Build() =>
                new DocBuilder().WithInput(inputRoot)
                    .WithOutput(outputRoot)
                    .UsePrivacy(static opts => opts with { FailOnError = true })
                    .BuildAsync();
        }
    }

    /// <summary>An explicit cache directory survives a clean build (i.e. fresh output dir): the second build hits the cache and never touches the network.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SecondBuildHitsCacheInsteadOfNetwork()
    {
        using var fixture = TempSite.Create();
        var cacheDir = Path.Combine(fixture.Root, "shared-cache");
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            server.AddRoute("/cached.png", "image/png", [.. "cached"u8]);
            server.Start();

            var url = $"{server.BaseUrl}cached.png";
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "page.md"), $"![]({url})\n");

            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .UsePrivacy(opts => opts.WithCacheDirectory(cacheDir))
                .BuildAsync();

            await Assert.That(server.HitCountFor("/cached.png")).IsEqualTo(1);

            // Fresh output dir simulates a clean build; the cache survives.
            var freshOutput = Path.Combine(fixture.Root, "_site2");
            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(freshOutput)
                .UsePrivacy(opts => opts.WithCacheDirectory(cacheDir))
                .BuildAsync();

            await Assert.That(server.HitCountFor("/cached.png")).IsEqualTo(1);
            var localized = Directory.GetFiles(Path.Combine(freshOutput, "assets", "external"));
            await Assert.That(localized).HasSingleItem();
        }
    }

    /// <summary>Polly retry recovers from a one-shot 503 followed by a 200.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PollyRetryRecoversFromTransientServerError()
    {
        using var fixture = TempSite.Create();
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            server.AddFlakyRoute("/flaky.png", "image/png", [.. "ok"u8], failuresBeforeSuccess: 1);
            server.Start();

            var url = $"{server.BaseUrl}flaky.png";
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "page.md"), $"![]({url})\n");

            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .UsePrivacy(static opts => opts with { MaxRetries = 2 })
                .BuildAsync();

            await Assert.That(server.HitCountFor("/flaky.png")).IsEqualTo(2);
            var localized = Directory.GetFiles(Path.Combine(fixture.Output, "assets", "external"));
            await Assert.That(localized).HasSingleItem();
        }
    }

    /// <summary>A 404 from the upstream is logged-and-swallowed; the build still succeeds.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildSurvivesUpstreamFailure()
    {
        using var fixture = TempSite.Create();
        LoopbackHttpServer server = new();
        await using (server.ConfigureAwait(false))
        {
            server.Start();
            var url = $"{server.BaseUrl}missing.png";
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "page.md"), $"![missing]({url})");

            await new DocBuilder()
                .WithInput(fixture.Root)
                .WithOutput(fixture.Output)
                .UsePrivacy()
                .BuildAsync();

            await Assert.That(File.Exists(Path.Combine(fixture.Output, "page.html"))).IsTrue();
        }
    }

    /// <summary>Disposable temp-directory fixture for end-to-end build tests.</summary>
    private sealed class TempSite : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempSite"/> class.</summary>
        /// <param name="root">Absolute path to the temp root.</param>
        private TempSite(string root)
        {
            Root = root;
            Output = Path.Combine(root, "_site");
        }

        /// <summary>Gets the absolute path to the temp root (also the docs input root).</summary>
        public string Root { get; }

        /// <summary>Gets the absolute path to the output directory.</summary>
        public string Output { get; }

        /// <summary>Creates a fresh temp tree.</summary>
        /// <returns>A new fixture; caller must dispose.</returns>
        public static TempSite Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "smkd-priv-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(root);
            return new(root);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>Tiny HttpListener-backed server for loopback fetches.</summary>
    private sealed class LoopbackHttpServer : IAsyncDisposable
    {
        /// <summary>Routes registered before <c>Start</c> is called.</summary>
        private readonly Dictionary<string, (string ContentType, byte[] Bytes)> _routes = new(StringComparer.Ordinal);

        /// <summary>Flaky routes that fail a fixed number of times before succeeding.</summary>
        private readonly Dictionary<string, (string ContentType, byte[] Bytes, int FailuresRemaining)> _flakyRoutes = new(StringComparer.Ordinal);

        /// <summary>Per-route hit counter.</summary>
        private readonly Dictionary<string, int> _hits = new(StringComparer.Ordinal);

        /// <summary>Underlying HTTP listener.</summary>
        private readonly HttpListener _listener = new();

        /// <summary>Cancellation source for the request pump.</summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Background task that pumps requests; null until <c>Start</c> runs.</summary>
        private Task? _pump;

        /// <summary>Gets the base URL of the running server (with trailing slash).</summary>
        public string BaseUrl { get; private set; } = string.Empty;

        /// <summary>Adds a route to be served once the listener is running.</summary>
        /// <param name="absolutePath">URL path (including the leading slash).</param>
        /// <param name="contentType">Response content-type.</param>
        /// <param name="bytes">Response body.</param>
        public void AddRoute(string absolutePath, string contentType, byte[] bytes)
        {
            _routes[absolutePath] = (contentType, bytes);
            _hits[absolutePath] = 0;
        }

        /// <summary>Adds a flaky route that returns 503 for the first <paramref name="failuresBeforeSuccess"/> requests, then the bytes.</summary>
        /// <param name="absolutePath">URL path.</param>
        /// <param name="contentType">Response content-type when the request finally succeeds.</param>
        /// <param name="bytes">Success response body.</param>
        /// <param name="failuresBeforeSuccess">Number of 503s to emit before the first success.</param>
        public void AddFlakyRoute(string absolutePath, string contentType, byte[] bytes, int failuresBeforeSuccess)
        {
            _flakyRoutes[absolutePath] = (contentType, bytes, failuresBeforeSuccess);
            _hits[absolutePath] = 0;
        }

        /// <summary>Picks a free port and starts the listener.</summary>
        public void Start()
        {
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(BaseUrl);
            _listener.Start();
            _pump = Task.Run(() => Pump(_cts.Token), _cts.Token);
        }

        /// <summary>Returns the number of requests served for <paramref name="absolutePath"/>.</summary>
        /// <param name="absolutePath">URL path.</param>
        /// <returns>Hit count.</returns>
        public int HitCountFor(string absolutePath) =>
            _hits.TryGetValue(absolutePath, out var count) ? count : 0;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed; nothing to do.
            }

            if (_pump is not null)
            {
                try
                {
                    await _pump.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation.
                }
                catch (HttpListenerException)
                {
                    // Expected when the listener stop races the pump.
                }
            }

            _cts.Dispose();
            _listener.Close();
        }

        /// <summary>Asks the OS for a free TCP port.</summary>
        /// <returns>An unused port number.</returns>
        private static int GetFreePort()
        {
            TcpListener l = new(IPAddress.Loopback, 0);
            l.Start();
            try
            {
                return ((IPEndPoint)l.LocalEndpoint).Port;
            }
            finally
            {
                l.Stop();
            }
        }

        /// <summary>Pumps requests until cancellation.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes on shutdown.</returns>
        private async Task Pump(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                Serve(context);
            }
        }

        /// <summary>Serves one request from the registered route map.</summary>
        /// <param name="context">Request context.</param>
        private void Serve(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            if (_hits.TryGetValue(path, out var count))
            {
                _hits[path] = count + 1;
            }

            if (_flakyRoutes.TryGetValue(path, out var flaky))
            {
                if (flaky.FailuresRemaining > 0)
                {
                    _flakyRoutes[path] = flaky with { FailuresRemaining = flaky.FailuresRemaining - 1 };
                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = flaky.ContentType;
                    context.Response.ContentLength64 = flaky.Bytes.Length;
                    context.Response.OutputStream.Write(flaky.Bytes, 0, flaky.Bytes.Length);
                }
            }
            else if (_routes.TryGetValue(path, out var route))
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = route.ContentType;
                context.Response.ContentLength64 = route.Bytes.Length;
                context.Response.OutputStream.Write(route.Bytes, 0, route.Bytes.Length);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            context.Response.Close();
        }
    }
}
