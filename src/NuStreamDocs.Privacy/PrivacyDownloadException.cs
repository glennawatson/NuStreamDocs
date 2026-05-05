// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy;

/// <summary>
/// Thrown from <see cref="PrivacyPlugin"/>'s finalize hook when one or
/// more external assets failed to download and
/// <see cref="PrivacyOptions.FailOnError"/> is set.
/// </summary>
public sealed class PrivacyDownloadException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PrivacyDownloadException"/> class.</summary>
    /// <param name="failedUrls">The URLs that failed to download.</param>
    public PrivacyDownloadException(string[] failedUrls)
        : base(BuildMessage(failedUrls))
    {
        ArgumentNullException.ThrowIfNull(failedUrls);
        FailedUrls = failedUrls;
    }

    /// <summary>Initializes a new instance of the <see cref="PrivacyDownloadException"/> class with a custom message.</summary>
    /// <param name="message">Exception message.</param>
    public PrivacyDownloadException(string message)
        : base(message) =>
        FailedUrls = [];

    /// <summary>Initializes a new instance of the <see cref="PrivacyDownloadException"/> class with a custom message and inner exception.</summary>
    /// <param name="message">Exception message.</param>
    /// <param name="innerException">Inner exception.</param>
    public PrivacyDownloadException(string message, Exception innerException)
        : base(message, innerException) =>
        FailedUrls = [];

    /// <summary>Initializes a new instance of the <see cref="PrivacyDownloadException"/> class with no failures.</summary>
    public PrivacyDownloadException()
        : this([])
    {
    }

    /// <summary>Gets the URLs that failed to download.</summary>
    public string[] FailedUrls { get; }

    /// <summary>Builds a human-readable summary of the failed downloads.</summary>
    /// <param name="failedUrls">URLs that failed.</param>
    /// <returns>The exception message.</returns>
    private static string BuildMessage(string[] failedUrls) =>
        failedUrls.Length switch
        {
            0 => "Privacy plugin: no failed downloads.",
            1 => $"Privacy plugin: 1 external asset failed to download: {failedUrls[0]}",
            _ => $"Privacy plugin: {failedUrls.Length} external assets failed to download (first: {failedUrls[0]})"
        };
}
