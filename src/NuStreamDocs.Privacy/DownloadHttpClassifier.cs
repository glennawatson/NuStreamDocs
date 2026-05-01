// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using Polly;

namespace NuStreamDocs.Privacy;

/// <summary>Pure classifiers used by <see cref="ExternalAssetDownloader"/> to decide retry vs CSS post-processing.</summary>
internal static class DownloadHttpClassifier
{
    /// <summary>HTTP status code that marks the boundary of "server error" responses worth retrying.</summary>
    private const int ServerErrorStatusFloor = 500;

    /// <summary>Returns true when the outcome is a transient failure worth retrying.</summary>
    /// <param name="outcome">Polly outcome.</param>
    /// <returns>True when the request should be retried (5xx, 408, 429, or any thrown <see cref="HttpRequestException"/> / <see cref="TaskCanceledException"/>).</returns>
    public static bool IsTransient(in Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TaskCanceledException)
        {
            return true;
        }

        var response = outcome.Result;
        if (response is null)
        {
            return false;
        }

        var code = (int)response.StatusCode;
        return response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || code >= ServerErrorStatusFloor;
    }

    /// <summary>Returns true when the response looks like a CSS file (extension or Content-Type).</summary>
    /// <param name="uri">Request URI.</param>
    /// <param name="response">HTTP response.</param>
    /// <returns>True when the body should run through <c>CssUrlRewriter</c>.</returns>
    public static bool LooksLikeCss(Uri uri, HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(response);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.Equals(contentType, "text/css", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = uri.AbsolutePath;
        return path.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
    }
}
