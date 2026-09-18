// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Branch-coverage tests for the ExternalAssetDownloader retry/CSS-detection helpers.</summary>
public class ExternalAssetDownloaderHelperTests
{
    /// <summary>Invalid request timeouts are rejected even when the registry is empty.</summary>
    /// <param name="milliseconds">Configured timeout in milliseconds.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(0D)]
    [Arguments(-2D)]
    [Arguments(2147483648D)]
    public async Task DownloadAllRejectsInvalidTimeout(double milliseconds)
    {
        var registry = new ExternalAssetRegistry([.. "assets"u8]);
        var filter = new HostFilter(null, null);
        var settings = new ExternalAssetDownloader.DownloadSettings(1, TimeSpan.FromMilliseconds(milliseconds), 1);
        await Assert.That(async () =>
        {
            _ = await ExternalAssetDownloader.DownloadAllAsync(registry, "/output", "/cache", settings, filter, NullLogger.Instance, CancellationToken.None);
        }).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>A finite or infinite request timeout is accepted for an empty download batch.</summary>
    /// <param name="milliseconds">Configured timeout in milliseconds.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(-1D)]
    [Arguments(1000D)]
    public async Task DownloadAllAcceptsValidTimeout(double milliseconds)
    {
        var registry = new ExternalAssetRegistry([.. "assets"u8]);
        var filter = new HostFilter(null, null);
        var settings = new ExternalAssetDownloader.DownloadSettings(1, TimeSpan.FromMilliseconds(milliseconds), 1);
        var failures = await ExternalAssetDownloader.DownloadAllAsync(registry, "/output", "/cache", settings, filter, NullLogger.Instance, CancellationToken.None);
        await Assert.That(failures).IsEmpty();
    }

    /// <summary>Server-error and rate-limited HTTP statuses are flagged as transient.</summary>
    /// <param name="status">HTTP status code.</param>
    /// <param name="expected">Expected IsTransient result.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(HttpStatusCode.RequestTimeout, true)]
    [Arguments(HttpStatusCode.TooManyRequests, true)]
    [Arguments(HttpStatusCode.InternalServerError, true)]
    [Arguments(HttpStatusCode.BadGateway, true)]
    [Arguments(HttpStatusCode.ServiceUnavailable, true)]
    [Arguments(HttpStatusCode.OK, false)]
    [Arguments(HttpStatusCode.NotFound, false)]
    [Arguments(HttpStatusCode.BadRequest, false)]
    public async Task IsTransientStatusCodes(HttpStatusCode status, bool expected)
    {
        using HttpResponseMessage response = new(status);
        var outcome = Outcome.FromResult(response);
        await Assert.That(DownloadHttpClassifier.IsTransient(outcome)).IsEqualTo(expected);
    }

    /// <summary>Transport exceptions are flagged as transient.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IsTransientHttpRequestException()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(new HttpRequestException("boom"));
        await Assert.That(DownloadHttpClassifier.IsTransient(outcome)).IsTrue();
    }

    /// <summary>Cancellation/timeouts are flagged as transient.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IsTransientTaskCanceledException()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(new TaskCanceledException());
        await Assert.That(DownloadHttpClassifier.IsTransient(outcome)).IsTrue();
    }

    /// <summary>Unrelated exceptions are not transient.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IsTransientOtherExceptionFalse()
    {
        var outcome = Outcome.FromException<HttpResponseMessage>(new InvalidOperationException());
        await Assert.That(DownloadHttpClassifier.IsTransient(outcome)).IsFalse();
    }

    /// <summary>A text/css Content-Type is detected as CSS.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooksLikeCssByContentType()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Content = new StringContent("body{}", Encoding.UTF8, "text/css");
        Uri uri = new("https://x.test/no-extension");
        await Assert.That(DownloadHttpClassifier.LooksLikeCss(uri, response)).IsTrue();
    }

    /// <summary>A .css path is detected as CSS even when Content-Type is generic.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooksLikeCssByExtension()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Content = new StringContent("body{}", Encoding.UTF8, "application/octet-stream");
        Uri uri = new("https://x.test/path/style.CSS");
        await Assert.That(DownloadHttpClassifier.LooksLikeCss(uri, response)).IsTrue();
    }

    /// <summary>Non-CSS URLs with non-CSS Content-Type are not detected as CSS.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooksLikeCssNeitherFalse()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Content = new StringContent("hi", Encoding.UTF8, "text/plain");
        Uri uri = new("https://x.test/page.html");
        await Assert.That(DownloadHttpClassifier.LooksLikeCss(uri, response)).IsFalse();
    }
}
