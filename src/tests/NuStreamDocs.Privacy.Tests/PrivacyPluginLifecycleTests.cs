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
    /// <summary>Retry allowance for the transient-error scenario.</summary>
    private const int RetryCount = 2;

    /// <summary>One failed request followed by one successful request.</summary>
    private const int ExpectedRequestCount = 2;

    /// <summary>Png Content Type used by the test cases.</summary>
    private const string PngContentType = "image/png";

    /// <summary>Page File Name used by the test cases.</summary>
    private const string PageFileName = "page.md";

    /// <summary>Assets Directory used by the test cases.</summary>
    private const string AssetsDirectory = "assets";

    /// <summary>External Directory used by the test cases.</summary>
    private const string ExternalDirectory = "external";

    /// <summary>Cached Image Path used by the test cases.</summary>
    private const string CachedImagePath = "/cached.png";

    /// <summary>Download Parallelism used by the test cases.</summary>
    private const int DownloadParallelism = 2;

    /// <summary>Download Timeout Seconds used by the test cases.</summary>
    private const int DownloadTimeoutSeconds = 60;

    /// <summary>Expected Asset Count used by the test cases.</summary>
    private const int ExpectedAssetCount = 2;

    /// <summary>A markdown page with an absolute <c>img</c> URL ends up referencing the local copy and the asset bytes are present on disk.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LocalizesExternalImageOnBuild()
    {
        using var fixture = TempSite.Create();
        await using var server = new LoopbackHttpServer();
        const string AssetText = "fake-png-bytes";
        server.AddRoute("/logo.png", PngContentType, (byte[])[.. "fake-png-bytes"u8]);
        server.Start();

        var pageUrl = $"{server.BaseUrl}logo.png";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, PageFileName), $"![logo]({pageUrl})\n");

        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePrivacy(static opts => opts with { DownloadParallelism = DownloadParallelism })
            .BuildAsync();

        var rendered = await File.ReadAllTextAsync(Path.Combine(fixture.Output, "page.html"));
        await Assert.That(rendered).Contains("/assets/external/");
        await Assert.That(rendered).DoesNotContain(pageUrl);

        var localized = Directory.GetFiles(Path.Combine(fixture.Output, AssetsDirectory, ExternalDirectory));
        await Assert.That(localized).HasSingleItem();
        var bytes = await File.ReadAllBytesAsync(localized[0]);
        await Assert.That(Encoding.UTF8.GetString(bytes)).IsEqualTo(AssetText);
    }

    /// <summary>The same external URL shared across two pages downloads exactly once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DedupesAcrossPages()
    {
        using var fixture = TempSite.Create();
        await using var server = new LoopbackHttpServer();
        server.AddRoute("/shared.png", PngContentType, [.. "shared"u8]);
        server.Start();

        var url = $"{server.BaseUrl}shared.png";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "a.md"), $"![]({url})");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "b.md"), $"![]({url})");

        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)

            // A loopback fetch never takes seconds — but a heavily-contended CI agent can stall one
            // past the default 10s timeout, which then retries and double-hits the server. Pin a
            // generous timeout so the hit count is deterministic; the de-dup (one registry entry)
            // is still what makes this exactly 1.
            .UsePrivacy(static opts => opts with { DownloadTimeout = TimeSpan.FromSeconds(DownloadTimeoutSeconds) })
            .BuildAsync();

        await Assert.That(server.HitCountFor("/shared.png")).IsEqualTo(1);
    }

    /// <summary>A CSS file referencing a font URL drives a second download pass; the font lands locally too.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FollowsNestedCssUrls()
    {
        using var fixture = TempSite.Create();
        await using var server = new LoopbackHttpServer();
        const string FontBytes = "FONT-DATA";
        server.Start();
        var fontUrl = $"{server.BaseUrl}font.woff2";
        server.AddRoute("/font.woff2", "font/woff2", (byte[])[.. "FONT-DATA"u8]);
        server.AddRoute("/fonts.css", "text/css", Encoding.UTF8.GetBytes($"@font-face {{ src: url({fontUrl}) }}"));

        var pageUrl = $"{server.BaseUrl}fonts.css";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, PageFileName), $"![]({pageUrl})\n");

        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePrivacy()
            .BuildAsync();

        var localized = Directory.GetFiles(Path.Combine(fixture.Output, AssetsDirectory, ExternalDirectory));
        await Assert.That(localized.Length).IsEqualTo(ExpectedAssetCount);

        var fontFile = Array.Find(localized, static f => f.EndsWith(".woff2", StringComparison.Ordinal));
        await Assert.That(fontFile).IsNotNull();
        await Assert.That(await File.ReadAllTextAsync(fontFile)).IsEqualTo(FontBytes);

        var cssFile = Array.Find(localized, static f => f.EndsWith(".css", StringComparison.Ordinal));
        await Assert.That(cssFile).IsNotNull();
        var rewrittenCss = await File.ReadAllTextAsync(cssFile);
        await Assert.That(rewrittenCss).Contains("/assets/external/");
        await Assert.That(rewrittenCss).DoesNotContain(fontUrl);
    }

    /// <summary>Audit-only mode writes a manifest of detected external URLs and never hits the network.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AuditOnlyModeEmitsManifestWithoutDownloading()
    {
        using var fixture = TempSite.Create();
        await using var server = new LoopbackHttpServer();
        server.AddRoute("/asset.png", PngContentType, [.. "should-not-be-fetched"u8]);
        server.Start();

        var url = $"{server.BaseUrl}asset.png";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, PageFileName), $"![]({url})\n");

        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePrivacy(static opts => opts with { AuditOnly = true })
            .BuildAsync();

        await Assert.That(server.HitCountFor("/asset.png")).IsEqualTo(0);
        await Assert.That(Directory.Exists(Path.Combine(fixture.Output, AssetsDirectory, ExternalDirectory))).IsFalse();

        var manifestPath = Path.Combine(fixture.Output, "privacy-audit.json");
        await Assert.That(File.Exists(manifestPath)).IsTrue();
        var manifest = await File.ReadAllTextAsync(manifestPath);
        await Assert.That(manifest).Contains(url);
        await Assert.That(manifest).Contains("\"auditOnly\": true");
    }

    /// <summary><c>PrivacyOptions.FailOnError</c> turns a 404 into a thrown <c>PrivacyDownloadException</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FailOnErrorRaisesOnUpstreamFailure()
    {
        using var fixture = TempSite.Create();
        await using var server = new LoopbackHttpServer();
        server.Start();
        var url = $"{server.BaseUrl}missing.png";
        var inputRoot = fixture.Root;
        var outputRoot = fixture.Output;
        await File.WriteAllTextAsync(Path.Combine(inputRoot, PageFileName), $"![]({url})");

        await Assert.That((Func<Task>)Build).Throws<PrivacyDownloadException>();

        Task Build() =>
            new DocBuilder().WithInput(inputRoot)
                .WithOutput(outputRoot)
                .UsePrivacy(static opts => opts with { FailOnError = true })
                .BuildAsync();
    }

    /// <summary>An explicit cache directory survives a clean build (i.e. fresh output dir): the second build hits the cache and never touches the network.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SecondBuildHitsCacheInsteadOfNetwork()
    {
        using var fixture = TempSite.Create();
        var cacheDir = Path.Combine(fixture.Root, "shared-cache");
        await using var server = new LoopbackHttpServer();
        server.AddRoute(CachedImagePath, PngContentType, [.. "cached"u8]);
        server.Start();

        var url = $"{server.BaseUrl}cached.png";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, PageFileName), $"![]({url})\n");

        // Generous timeout: a contended CI agent can stall a loopback fetch past the default 10s
        // timeout, which then retries and double-hits the server. Pinning it keeps the hit count
        // deterministic so the cache-vs-network assertions below stay meaningful.
        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePrivacy(opts =>
                opts.WithCacheDirectory(cacheDir) with { DownloadTimeout = TimeSpan.FromSeconds(DownloadTimeoutSeconds) })
            .BuildAsync();

        await Assert.That(server.HitCountFor(CachedImagePath)).IsEqualTo(1);

        // Fresh output dir simulates a clean build; the cache survives.
        var freshOutput = Path.Combine(fixture.Root, "_site2");
        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(freshOutput)
            .UsePrivacy(opts =>
                opts.WithCacheDirectory(cacheDir) with { DownloadTimeout = TimeSpan.FromSeconds(DownloadTimeoutSeconds) })
            .BuildAsync();

        await Assert.That(server.HitCountFor(CachedImagePath)).IsEqualTo(1);
        var localized = Directory.GetFiles(Path.Combine(freshOutput, AssetsDirectory, ExternalDirectory));
        await Assert.That(localized).HasSingleItem();
    }

    /// <summary>Polly retry recovers from a one-shot 503 followed by a 200.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PollyRetryRecoversFromTransientServerError()
    {
        using var fixture = TempSite.Create();
        await using var server = new LoopbackHttpServer();
        server.AddFlakyRoute("/flaky.png", PngContentType, [.. "ok"u8], 1);
        server.Start();

        var url = $"{server.BaseUrl}flaky.png";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, PageFileName), $"![]({url})\n");

        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePrivacy(static opts => opts with { MaxRetries = RetryCount })
            .BuildAsync();

        await Assert.That(server.HitCountFor("/flaky.png")).IsEqualTo(ExpectedRequestCount);
        var localized = Directory.GetFiles(Path.Combine(fixture.Output, AssetsDirectory, ExternalDirectory));
        await Assert.That(localized).HasSingleItem();
    }

    /// <summary>A 404 from the upstream is logged-and-swallowed; the build still succeeds.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildSurvivesUpstreamFailure()
    {
        using var fixture = TempSite.Create();
        await using var server = new LoopbackHttpServer();
        server.Start();
        var url = $"{server.BaseUrl}missing.png";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, PageFileName), $"![missing]({url})");

        await new DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePrivacy()
            .BuildAsync();

        await Assert.That(File.Exists(Path.Combine(fixture.Output, "page.html"))).IsTrue();
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
            var root = Path.Combine(
                Path.GetTempPath(),
                $"smkd-priv-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
            _ = Directory.CreateDirectory(root);
            return new(root);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>Single-bind TcpListener loopback HTTP/1.1 server avoiding HttpListener port-race and disposal flakes.</summary>
    private sealed class LoopbackHttpServer : IAsyncDisposable
    {
        /// <summary>Read buffer size for incoming loopback HTTP request headers.</summary>
        private const int HeaderBufferSize = 4096;

        /// <summary>HTTP 200 status code.</summary>
        private const int StatusOk = 200;

        /// <summary>HTTP 404 status code.</summary>
        private const int StatusNotFound = 404;

        /// <summary>HTTP 503 status code.</summary>
        private const int StatusServiceUnavailable = 503;

        /// <summary>Synchronizes route tables and hit counters across concurrent connections.</summary>
        private readonly Lock _gate = new();

        /// <summary>Routes registered for the server.</summary>
        private readonly Dictionary<string, (string ContentType, byte[] Bytes)> _routes = [with(StringComparer.Ordinal)];

        /// <summary>Flaky routes that fail a fixed number of times before succeeding.</summary>
        private readonly Dictionary<string, (string ContentType, byte[] Bytes, int FailuresRemaining)> _flakyRoutes =
            [with(StringComparer.Ordinal)];

        /// <summary>Per-route hit counter.</summary>
        private readonly Dictionary<string, int> _hits = [with(StringComparer.Ordinal)];

        /// <summary>Single-bind loopback TCP listener.</summary>
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

        /// <summary>Cancellation source for the accept and connection tasks.</summary>
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Background accept loop task.</summary>
        private Task? _pump;

        /// <summary>Gets the base URL of the running server (with trailing slash).</summary>
        public string BaseUrl { get; private set; } = string.Empty;

        /// <summary>Adds a route to be served once the listener is running.</summary>
        /// <param name="absolutePath">URL path (including the leading slash).</param>
        /// <param name="contentType">Response content-type.</param>
        /// <param name="bytes">Response body.</param>
        public void AddRoute(string absolutePath, string contentType, byte[] bytes)
        {
            lock (_gate)
            {
                _routes[absolutePath] = (contentType, bytes);
                _hits[absolutePath] = 0;
            }
        }

        /// <summary>Adds a flaky route that returns 503 for the first <paramref name="failuresBeforeSuccess"/> requests, then the bytes.</summary>
        /// <param name="absolutePath">URL path.</param>
        /// <param name="contentType">Response content-type when the request finally succeeds.</param>
        /// <param name="bytes">Success response body.</param>
        /// <param name="failuresBeforeSuccess">Number of 503s to emit before the first success.</param>
        public void AddFlakyRoute(string absolutePath, string contentType, byte[] bytes, int failuresBeforeSuccess)
        {
            lock (_gate)
            {
                _flakyRoutes[absolutePath] = (contentType, bytes, failuresBeforeSuccess);
                _hits[absolutePath] = 0;
            }
        }

        /// <summary>Binds an OS-assigned loopback port atomically and starts accepting HTTP requests.</summary>
        public void Start()
        {
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUrl = $"http://127.0.0.1:{port}/";
            _pump = PumpAsync(_cts.Token);
        }

        /// <summary>Returns the number of requests served for <paramref name="absolutePath"/>.</summary>
        /// <param name="absolutePath">URL path.</param>
        /// <returns>Hit count.</returns>
        public int HitCountFor(string absolutePath)
        {
            lock (_gate)
            {
                return _hits.TryGetValue(absolutePath, out var count) ? count : 0;
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            _listener.Stop();

            if (_pump is not null)
            {
                await _pump.ConfigureAwait(false);
            }

            _cts.Dispose();
            _listener.Dispose();
        }

        /// <summary>Extracts the request path from the first line of raw HTTP request headers.</summary>
        /// <param name="headerBytes">UTF-8 header bytes.</param>
        /// <returns>Normalized URL path.</returns>
        private static string ParseRequestPath(ReadOnlySpan<byte> headerBytes)
        {
            var firstSpace = headerBytes.IndexOf((byte)' ');
            if (firstSpace < 0)
            {
                return "/";
            }

            var afterMethod = headerBytes[(firstSpace + 1)..];
            var endTarget = afterMethod.IndexOfAny((byte)' ', (byte)'\r', (byte)'\n');
            var rawTarget = endTarget < 0 ? afterMethod : afterMethod[..endTarget];
            var queryIdx = rawTarget.IndexOf((byte)'?');
            var pathSlice = queryIdx < 0 ? rawTarget : rawTarget[..queryIdx];
            return pathSlice.IsEmpty ? "/" : Encoding.UTF8.GetString(pathSlice);
        }

        /// <summary>Reads from <paramref name="stream"/> until the <c>\r\n\r\n</c> header terminator is seen.</summary>
        /// <param name="stream">Connected client stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Accumulated header bytes, or empty on disconnect.</returns>
        private static async Task<byte[]> ReadHeadersAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[HeaderBufferSize];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (buffer.AsSpan(0, total).IndexOf("\r\n\r\n"u8) >= 0)
                {
                    break;
                }
            }

            return buffer[..total];
        }

        /// <summary>Accepts loopback clients until cancelled and awaits in-flight client handlers.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task completing after shutdown.</returns>
        private async Task PumpAsync(CancellationToken cancellationToken)
        {
            List<Task> activeClients = [];
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                activeClients.Add(HandleClientAsync(client, cancellationToken));
            }

            for (var i = 0; i < activeClients.Count; i++)
            {
                await activeClients[i].ConfigureAwait(false);
            }
        }

        /// <summary>Serves a single HTTP/1.1 request over <paramref name="client"/> and closes the connection.</summary>
        /// <param name="client">Accepted TCP client.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async task.</returns>
        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                try
                {
                    await using var stream = client.GetStream();
                    var headers = await ReadHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
                    if (headers.Length == 0)
                    {
                        return;
                    }

                    var path = ParseRequestPath(headers);
                    var (statusCode, reason, contentType, body) = ResolveRoute(path);
                    var responseHead = Encoding.ASCII.GetBytes(
                        $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(responseHead, cancellationToken).ConfigureAwait(false);
                    if (body.Length > 0)
                    {
                        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                    }

                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during server shutdown.
                }
                catch (IOException)
                {
                    // Expected if client closes early.
                }
                catch (SocketException)
                {
                    // Expected if socket resets during teardown.
                }
                catch (ObjectDisposedException)
                {
                    // Expected if stream disposes concurrently with shutdown.
                }
            }
        }

        /// <summary>Resolves the HTTP response tuple and increments hit counters under lock.</summary>
        /// <param name="path">Request path.</param>
        /// <returns>Status code, reason phrase, content type, and payload bytes.</returns>
        private (int StatusCode, string Reason, string ContentType, byte[] Body) ResolveRoute(string path)
        {
            lock (_gate)
            {
                if (_hits.TryGetValue(path, out var count))
                {
                    _hits[path] = count + 1;
                }

                if (_flakyRoutes.TryGetValue(path, out var flaky))
                {
                    if (flaky.FailuresRemaining > 0)
                    {
                        _flakyRoutes[path] = flaky with { FailuresRemaining = flaky.FailuresRemaining - 1 };
                        return (StatusServiceUnavailable, "Service Unavailable", "text/plain", []);
                    }

                    return (StatusOk, "OK", flaky.ContentType, flaky.Bytes);
                }

                return _routes.TryGetValue(path, out var route)
                    ? (StatusOk, "OK", route.ContentType, route.Bytes)
                    : (StatusNotFound, "Not Found", "text/plain", []);
            }
        }
    }
}
