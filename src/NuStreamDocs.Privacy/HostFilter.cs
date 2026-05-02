// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Per-URL gate combining the host-level and URL-pattern allow / skip
/// rules from <see cref="PrivacyOptions"/> into a single
/// <see cref="ShouldLocalize(System.ReadOnlySpan{byte})"/> check.
/// </summary>
/// <remarks>
/// Decision order:
/// <list type="number">
/// <item>Reject anything that isn't an absolute http(s) URL.</item>
/// <item>Reject when the host is on the skip list.</item>
/// <item>Reject when the URL matches an exclude pattern.</item>
/// <item>Accept when an include pattern matches (when any are configured).</item>
/// <item>Accept when the host is on the allow list (when any hosts are configured).</item>
/// <item>Accept when neither allow-side rule is configured.</item>
/// </list>
/// All host comparisons are case-insensitive ASCII; patterns honor the
/// simple <c>*</c>/<c>?</c> glob semantics in <see cref="UrlPatternMatcher"/>.
/// The byte overload is the production hot path — it reaches a verdict
/// without UTF-16 transcoding for the common scheme + skip-list +
/// allow-list rules, only decoding when URL-level globs are
/// configured.
/// </remarks>
internal sealed class HostFilter
{
    /// <summary>Pre-encoded lowercase UTF-8 bytes of the skip list, for the byte fast-reject path.</summary>
    private readonly byte[][] _hostsToSkipBytes;

    /// <summary>Pre-encoded lowercase UTF-8 bytes of the allow list, for the byte fast-accept path.</summary>
    private readonly byte[][] _hostsAllowedBytes;

    /// <summary>True when the allow list is non-empty.</summary>
    private readonly bool _hasAllowedHosts;

    /// <summary>URL-level include patterns; non-empty broadens the allow set beyond the byte allow list.</summary>
    private readonly UrlPatternMatcher _includePatterns;

    /// <summary>URL-level exclude patterns; matched URLs are dropped even when the host passes.</summary>
    private readonly UrlPatternMatcher _excludePatterns;

    /// <summary>Initializes a new instance of the <see cref="HostFilter"/> class.</summary>
    /// <param name="hostsToSkip">Skip list (may be empty).</param>
    /// <param name="hostsAllowed">Allow list (empty means "everything not on the skip list").</param>
    public HostFilter(string[]? hostsToSkip, string[]? hostsAllowed)
        : this(hostsToSkip, hostsAllowed, includePatterns: null, excludePatterns: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HostFilter"/> class.</summary>
    /// <param name="hostsToSkip">Skip list (may be empty).</param>
    /// <param name="hostsAllowed">Allow list (empty means "everything not on the skip list").</param>
    /// <param name="includePatterns">URL-level include glob patterns (empty disables).</param>
    /// <param name="excludePatterns">URL-level exclude glob patterns (empty disables).</param>
    public HostFilter(
        string[]? hostsToSkip,
        string[]? hostsAllowed,
        string[]? includePatterns,
        string[]? excludePatterns)
    {
        _hostsToSkipBytes = ToLowerUtf8Array(hostsToSkip);
        _hostsAllowedBytes = ToLowerUtf8Array(hostsAllowed);
        _hasAllowedHosts = _hostsAllowedBytes.Length > 0;
        _includePatterns = new(includePatterns);
        _excludePatterns = new(excludePatterns);
    }

    /// <summary>Returns true when <paramref name="urlBytes"/> is an absolute http(s) URL whose host passes the configured allow/skip rules.</summary>
    /// <param name="urlBytes">UTF-8 URL slice from the source page.</param>
    /// <returns>True when the URL should be localized.</returns>
    public bool ShouldLocalize(ReadOnlySpan<byte> urlBytes)
    {
        if (!TryExtractHttpHost(urlBytes, out var host))
        {
            return false;
        }

        if (HostMatches(_hostsToSkipBytes, host))
        {
            return false;
        }

        // URL-level pattern matching is char-based; decode once when patterns are configured.
        if (_excludePatterns.HasPatterns || _includePatterns.HasPatterns)
        {
            var url = Encoding.UTF8.GetString(urlBytes);
            if (_excludePatterns.IsMatch(url))
            {
                return false;
            }

            if (_includePatterns.HasPatterns && _includePatterns.IsMatch(url))
            {
                return true;
            }
        }

        return !_hasAllowedHosts || HostMatches(_hostsAllowedBytes, host);
    }

    /// <summary>String adapter for callers outside the byte hot path (tests, configure-time validators).</summary>
    /// <param name="url">Candidate URL string.</param>
    /// <returns>True when the URL should be localized.</returns>
    /// <remarks>
    /// Encodes <paramref name="url"/> into a stack-or-pool buffer via
    /// <see cref="Utf8StackBuffer"/> and delegates to the byte overload —
    /// single UTF-8 encode per call, zero heap allocation when the URL
    /// fits in the stack buffer.
    /// </remarks>
    public bool ShouldLocalize(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        using var buf = new Utf8StackBuffer(url, stackalloc byte[Utf8StackBuffer.StackSize]);
        return ShouldLocalize(buf.Bytes);
    }

    /// <summary>Lower-cases a host array and encodes each entry to UTF-8 bytes, returning <c>[]</c> when the input is null/empty.</summary>
    /// <param name="hosts">Host names; may be null.</param>
    /// <returns>Pre-encoded lowercase byte arrays.</returns>
    private static byte[][] ToLowerUtf8Array(string[]? hosts)
    {
        if (hosts is null or [])
        {
            return [];
        }

        var result = new byte[hosts.Length][];
        for (var i = 0; i < hosts.Length; i++)
        {
            result[i] = Encoding.UTF8.GetBytes(hosts[i].ToLowerInvariant());
        }

        return result;
    }

    /// <summary>True when <paramref name="host"/> equals any entry of <paramref name="lowerEntries"/> ignoring ASCII case.</summary>
    /// <param name="lowerEntries">Lowercase byte-array entries.</param>
    /// <param name="host">Host bytes from the candidate URL.</param>
    /// <returns>True when matched.</returns>
    private static bool HostMatches(byte[][] lowerEntries, ReadOnlySpan<byte> host)
    {
        for (var i = 0; i < lowerEntries.Length; i++)
        {
            if (AsciiByteHelpers.EqualsIgnoreAsciiCase(host, lowerEntries[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tries to extract the host portion from <paramref name="urlBytes"/> when the URL is an absolute http/https URL.</summary>
    /// <param name="urlBytes">UTF-8 URL slice.</param>
    /// <param name="host">Host bytes (no port, no path) on success.</param>
    /// <returns>True when the URL starts with <c>http://</c> or <c>https://</c> and a host is present.</returns>
    private static bool TryExtractHttpHost(ReadOnlySpan<byte> urlBytes, out ReadOnlySpan<byte> host)
    {
        host = default;
        var schemeLength = SchemePrefixLength(urlBytes);
        if (schemeLength is 0)
        {
            return false;
        }

        var rest = urlBytes[schemeLength..];

        // Strip optional userinfo prefix ("user:pass@").
        var at = rest.IndexOf((byte)'@');
        if (at >= 0)
        {
            rest = rest[(at + 1)..];
        }

        // Host ends at the first ':' (port), '/' (path), '?' (query), or '#' (fragment).
        var endIdx = rest.IndexOfAny((byte)':', (byte)'/', (byte)'?');
        var hashIdx = rest.IndexOf((byte)'#');
        if (hashIdx >= 0 && (endIdx < 0 || hashIdx < endIdx))
        {
            endIdx = hashIdx;
        }

        var hostSlice = endIdx < 0 ? rest : rest[..endIdx];
        if (hostSlice.IsEmpty)
        {
            return false;
        }

        host = hostSlice;
        return true;
    }

    /// <summary>Returns 7 / 8 when <paramref name="urlBytes"/> starts with <c>http://</c> / <c>https://</c> (case-insensitive ASCII), or 0 otherwise.</summary>
    /// <param name="urlBytes">UTF-8 URL slice.</param>
    /// <returns>Byte count to skip past the scheme delimiter, or 0 when the scheme isn't recognized.</returns>
    private static int SchemePrefixLength(ReadOnlySpan<byte> urlBytes)
    {
        if (AsciiByteHelpers.StartsWithIgnoreAsciiCase(urlBytes, 0, "https://"u8))
        {
            return "https://"u8.Length;
        }

        if (AsciiByteHelpers.StartsWithIgnoreAsciiCase(urlBytes, 0, "http://"u8))
        {
            return "http://"u8.Length;
        }

        return 0;
    }
}
