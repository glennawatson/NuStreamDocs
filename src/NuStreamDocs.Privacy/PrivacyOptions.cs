// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy;

/// <summary>Configuration for <see cref="PrivacyPlugin"/>.</summary>
/// <remarks>Mirrors the most-used knobs of mkdocs-material's privacy plugin, scoped to asset externalization.</remarks>
/// <param name="Enabled">Master switch. When false, the plugin is a no-op.</param>
/// <param name="AssetDirectory">Forward-slash relative directory the externalized assets are written to (default <c>assets/external</c>).</param>
/// <param name="DownloadParallelism">Maximum number of concurrent HTTP downloads.</param>
/// <param name="DownloadTimeout">Per-request HTTP timeout.</param>
/// <param name="HostsToSkip">Hosts that should be left as external links (e.g. CDN domains the consumer trusts to stay up). Empty by default.</param>
/// <param name="HostsAllowed">When non-empty, only URLs whose host matches this list are localized. Empty
/// (the default) means "localize everything not on <see cref="HostsToSkip"/>".</param>
/// <param name="AuditOnly">When true, the plugin scans pages and records the external-URL set without
/// rewriting HTML or downloading anything. Useful for compliance review without committing to
/// localization.</param>
/// <param name="AuditManifestPath">Forward-slash relative path under the output root where the
/// audit manifest is written. Empty disables manifest emission.</param>
/// <param name="AddRelNoOpener">When true, every external <c>&lt;a href&gt;</c> gains
/// <c>rel="noopener noreferrer"</c> so the destination can't read <c>window.opener</c> or
/// the referrer header.</param>
/// <param name="AddTargetBlank">When true, every external <c>&lt;a href&gt;</c> gains
/// <c>target="_blank"</c> so external links open in a new tab. Pairs with
/// <see cref="AddRelNoOpener"/>.</param>
/// <param name="UpgradeMixedContent">When true, <c>http://</c> URLs in asset attributes and
/// anchors are rewritten to <c>https://</c> before further processing. Stops mixed-content
/// blocking on HTTPS sites and prevents downgrade-attack vectors.</param>
/// <param name="FailOnError">When true, any failed asset download (network error, non-2xx
/// response) raises an exception during finalize; false (the default) swallows individual
/// failures so a single broken asset doesn't fail the whole build.</param>
/// <param name="CacheDirectory">Absolute path to the on-disk cache that survives <c>clean</c>
/// builds. Empty (the default) falls back to <c>{outputRoot}/.cache/privacy</c>. When set,
/// every fetched asset is written to this directory first and copied into the output root
/// from there.</param>
/// <param name="MaxRetries">Maximum retry attempts per asset on transient HTTP failures
/// (5xx responses, connection errors, timeouts). Zero disables retries.</param>
/// <param name="UrlIncludePatterns">URL-level glob patterns (<c>*</c>/<c>?</c>) that broaden
/// the allow set beyond <see cref="HostsAllowed"/>. Useful for opting in to specific
/// paths on a host whose other paths should remain external (e.g. localize
/// <c>https://fonts.googleapis.com/css*</c> while leaving the rest external).</param>
/// <param name="UrlExcludePatterns">URL-level glob patterns that drop matched URLs even when
/// the host would otherwise pass. Useful for fencing off endpoints (e.g.
/// <c>https://*.example/recaptcha/*</c>) that must stay first-party.</param>
/// <param name="GenerateCspManifest">When true, the plugin computes SHA-256 hashes of every
/// inline <c>&lt;style&gt;</c> and <c>&lt;script&gt;</c> body it sees and writes them to
/// <see cref="CspManifestPath"/>. Pair with a server header to deploy a strict CSP.</param>
/// <param name="CspManifestPath">Forward-slash relative path under the output root where the
/// CSP-hash manifest is written. Empty disables emission even when
/// <see cref="GenerateCspManifest"/> is set.</param>
public readonly record struct PrivacyOptions(
    bool Enabled,
    string AssetDirectory,
    int DownloadParallelism,
    TimeSpan DownloadTimeout,
    string[] HostsToSkip,
    string[] HostsAllowed,
    bool AuditOnly,
    string AuditManifestPath,
    bool AddRelNoOpener,
    bool AddTargetBlank,
    bool UpgradeMixedContent,
    bool FailOnError,
    string CacheDirectory,
    int MaxRetries,
    string[] UrlIncludePatterns,
    string[] UrlExcludePatterns,
    bool GenerateCspManifest,
    string CspManifestPath)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static PrivacyOptions Default { get; } = new(
        Enabled: true,
        AssetDirectory: "assets/external",
        DownloadParallelism: 4,
        DownloadTimeout: TimeSpan.FromSeconds(10),
        HostsToSkip: [],
        HostsAllowed: [],
        AuditOnly: false,
        AuditManifestPath: "privacy-audit.json",
        AddRelNoOpener: true,
        AddTargetBlank: false,
        UpgradeMixedContent: true,
        FailOnError: false,
        CacheDirectory: string.Empty,
        MaxRetries: 3,
        UrlIncludePatterns: [],
        UrlExcludePatterns: [],
        GenerateCspManifest: false,
        CspManifestPath: "csp-hashes.json");
}
