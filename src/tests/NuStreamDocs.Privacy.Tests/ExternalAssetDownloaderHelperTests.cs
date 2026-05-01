// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Polly;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Branch-coverage tests for the ExternalAssetDownloader retry/CSS-detection helpers.</summary>
public class ExternalAssetDownloaderHelperTests
{
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
        using var response = new HttpResponseMessage(status);
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
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("body{}", System.Text.Encoding.UTF8, "text/css"),
        };
        var uri = new Uri("https://x.test/no-extension");
        await Assert.That(DownloadHttpClassifier.LooksLikeCss(uri, response)).IsTrue();
    }

    /// <summary>A .css path is detected as CSS even when Content-Type is generic.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooksLikeCssByExtension()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("body{}", System.Text.Encoding.UTF8, "application/octet-stream"),
        };
        var uri = new Uri("https://x.test/path/style.CSS");
        await Assert.That(DownloadHttpClassifier.LooksLikeCss(uri, response)).IsTrue();
    }

    /// <summary>Non-CSS URLs with non-CSS Content-Type are not detected as CSS.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooksLikeCssNeitherFalse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("hi", System.Text.Encoding.UTF8, "text/plain"),
        };
        var uri = new Uri("https://x.test/page.html");
        await Assert.That(DownloadHttpClassifier.LooksLikeCss(uri, response)).IsFalse();
    }
}
