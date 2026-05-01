// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy;

/// <summary>
/// Per-URL gate combining the host-level and URL-pattern allow / skip
/// rules from <see cref="PrivacyOptions"/> into a single
/// <see cref="ShouldLocalise(string)"/> check.
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
/// All host lookups are case-insensitive; patterns honour the simple
/// <c>*</c>/<c>?</c> glob semantics in <see cref="UrlPatternMatcher"/>.
/// </remarks>
internal sealed class HostFilter
{
    /// <summary>Hosts the user has explicitly opted-out of localising. Per-instance and small (typically &lt; 32 entries), so a plain <see cref="HashSet{T}"/> is the right shape.</summary>
    private readonly HashSet<string> _hostsToSkip;

    /// <summary>Hosts the user has explicitly opted in (when non-empty, restricts the localise set to this list). Per-instance and small, plain <see cref="HashSet{T}"/>.</summary>
    private readonly HashSet<string> _hostsAllowed;

    /// <summary>URL-level include patterns; non-empty broadens the allow set beyond <see cref="_hostsAllowed"/>.</summary>
    private readonly UrlPatternMatcher _includePatterns;

    /// <summary>URL-level exclude patterns; matched URLs are dropped even when the host passes.</summary>
    private readonly UrlPatternMatcher _excludePatterns;

    /// <summary>Initializes a new instance of the <see cref="HostFilter"/> class.</summary>
    /// <param name="hostsToSkip">Skip list (may be empty).</param>
    /// <param name="hostsAllowed">Allow list (empty means "everything not on the skip list").</param>
    public HostFilter(
        string[]? hostsToSkip,
        string[]? hostsAllowed)
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
        _hostsToSkip = new(hostsToSkip ?? [], StringComparer.OrdinalIgnoreCase);
        _hostsAllowed = new(hostsAllowed ?? [], StringComparer.OrdinalIgnoreCase);
        _includePatterns = new(includePatterns);
        _excludePatterns = new(excludePatterns);
    }

    /// <summary>Returns true when <paramref name="url"/> is an absolute http(s) URL whose host passes the configured allow/skip rules.</summary>
    /// <param name="url">Candidate URL.</param>
    /// <returns>True when the URL should be localised.</returns>
    public bool ShouldLocalise(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (_hostsToSkip.Contains(uri.Host) || _excludePatterns.IsMatch(url))
        {
            return false;
        }

        if (_includePatterns.HasPatterns && _includePatterns.IsMatch(url))
        {
            return true;
        }

        if (_hostsAllowed.Count is 0 && !_includePatterns.HasPatterns)
        {
            return true;
        }

        return _hostsAllowed.Contains(uri.Host);
    }
}
