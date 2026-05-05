// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Privacy;

/// <summary>
/// A filter that accepts or rejects URLs based on their host name.
/// </summary>
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
    /// <param name="hostsToSkip">UTF-8 skip list (may be null/empty).</param>
    /// <param name="hostsAllowed">UTF-8 allow list (empty means "everything not on the skip list").</param>
    public HostFilter(byte[][]? hostsToSkip, byte[][]? hostsAllowed)
        : this(hostsToSkip, hostsAllowed, includePatterns: null, excludePatterns: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HostFilter"/> class.</summary>
    /// <param name="hostsToSkip">UTF-8 skip list (may be null/empty).</param>
    /// <param name="hostsAllowed">UTF-8 allow list (empty means "everything not on the skip list").</param>
    /// <param name="includePatterns">UTF-8 URL-level include glob patterns (empty disables).</param>
    /// <param name="excludePatterns">UTF-8 URL-level exclude glob patterns (empty disables).</param>
    public HostFilter(
        byte[][]? hostsToSkip,
        byte[][]? hostsAllowed,
        byte[][]? includePatterns,
        byte[][]? excludePatterns)
    {
        _hostsToSkipBytes = ToLowerCopy(hostsToSkip);
        _hostsAllowedBytes = ToLowerCopy(hostsAllowed);
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

        if (_excludePatterns.HasPatterns && _excludePatterns.IsMatch(urlBytes))
        {
            return false;
        }

        return (_includePatterns.HasPatterns && _includePatterns.IsMatch(urlBytes))
            || !_hasAllowedHosts || HostMatches(_hostsAllowedBytes, host);
    }

    /// <summary>Returns a fresh ASCII-lowercased copy of <paramref name="hosts"/>; <c>[]</c> when the input is null/empty.</summary>
    /// <param name="hosts">UTF-8 host names; may be null.</param>
    /// <returns>Lowercased byte arrays.</returns>
    /// <remarks>The byte path uses the lowercase form as the comparand for <see cref="AsciiByteHelpers.EqualsIgnoreAsciiCase"/>, which expects its second argument lowercase.</remarks>
    private static byte[][] ToLowerCopy(byte[][]? hosts)
    {
        if (hosts is null or [])
        {
            return [];
        }

        const byte AsciiCaseBit = 0x20;
        var result = new byte[hosts.Length][];
        for (var i = 0; i < hosts.Length; i++)
        {
            var src = hosts[i];
            var dst = new byte[src.Length];
            for (var j = 0; j < src.Length; j++)
            {
                var b = src[j];
                dst[j] = b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b | AsciiCaseBit) : b;
            }

            result[i] = dst;
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

        return AsciiByteHelpers.StartsWithIgnoreAsciiCase(urlBytes, 0, "http://"u8) ? "http://"u8.Length : 0;
    }
}
