// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator;

/// <summary>Configuration for the external HTTP link checker.</summary>
/// <param name="MaxRequestsPerHost">Maximum permits in the per-host rate-limit window.</param>
/// <param name="WindowSeconds">Length of the rate-limit window in seconds.</param>
/// <param name="MaxConcurrencyPerHost">Per-host concurrency cap.</param>
/// <param name="MaxRetries">Retry attempts on transient failures (5xx, network errors).</param>
/// <param name="RequestTimeoutSeconds">Per-request HTTP timeout.</param>
/// <param name="UserAgent">User-Agent header sent with each request.</param>
public sealed record ExternalLinkValidatorOptions(
    int MaxRequestsPerHost,
    int WindowSeconds,
    int MaxConcurrencyPerHost,
    int MaxRetries,
    int RequestTimeoutSeconds,
    string UserAgent)
{
    /// <summary>Default cap on requests per host per window.</summary>
    private const int DefaultRequestsPerHost = 8;

    /// <summary>Default rate-limit window length, in seconds.</summary>
    private const int DefaultWindowSeconds = 5;

    /// <summary>Default per-host concurrency cap.</summary>
    private const int DefaultConcurrencyPerHost = 4;

    /// <summary>Default retry-attempt count.</summary>
    private const int DefaultRetries = 3;

    /// <summary>Default per-request HTTP timeout in seconds.</summary>
    private const int DefaultRequestTimeoutSeconds = 30;

    /// <summary>Default User-Agent header.</summary>
    private const string DefaultUserAgent = "NuStreamDocs-LinkValidator/1.0";

    /// <summary>Gets the default options — 8 requests / 5s per host, 4 concurrent, 3 retries, 30s timeout.</summary>
    public static ExternalLinkValidatorOptions Default { get; } = new(
        DefaultRequestsPerHost,
        DefaultWindowSeconds,
        DefaultConcurrencyPerHost,
        DefaultRetries,
        DefaultRequestTimeoutSeconds,
        DefaultUserAgent);

    /// <summary>Throws when any field is invalid.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When a positive field is non-positive.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxRequestsPerHost);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WindowSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrencyPerHost);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxRetries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(RequestTimeoutSeconds);
        ArgumentException.ThrowIfNullOrWhiteSpace(UserAgent);
    }
}
