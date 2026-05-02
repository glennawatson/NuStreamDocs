// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy;

/// <summary>Configuration for <see cref="PrivacyPlugin"/>.</summary>
/// <remarks>
/// UTF-8 throughout: paths and host/pattern lists are <see cref="byte"/> arrays so the per-page
/// filtering hot path stays byte-shaped end-to-end. String-shaped construction helpers live on
/// <see cref="PrivacyOptionsExtensions"/> (encode-once at construction) for callers that build
/// from configuration files or test literals.
/// </remarks>
/// <param name="Enabled">Master switch. When false, the plugin is a no-op.</param>
/// <param name="AssetDirectory">UTF-8 forward-slash relative directory the externalized assets are written to (default <c>assets/external</c>).</param>
/// <param name="DownloadParallelism">Maximum number of concurrent HTTP downloads.</param>
/// <param name="DownloadTimeout">Per-request HTTP timeout.</param>
/// <param name="HostsToSkip">UTF-8 hosts that should be left as external links. Empty by default.</param>
/// <param name="HostsAllowed">When non-empty, only URLs whose host matches this UTF-8 list are localized.</param>
/// <param name="AuditOnly">When true, the plugin scans pages and records the external-URL set without rewriting HTML or downloading anything.</param>
/// <param name="AuditManifestPath">UTF-8 forward-slash relative path under the output root where the audit manifest is written. Empty disables emission.</param>
/// <param name="AddRelNoOpener">When true, every external <c>&lt;a href&gt;</c> gains <c>rel="noopener noreferrer"</c>.</param>
/// <param name="AddTargetBlank">When true, every external <c>&lt;a href&gt;</c> gains <c>target="_blank"</c>.</param>
/// <param name="UpgradeMixedContent">When true, <c>http://</c> URLs are rewritten to <c>https://</c> before further processing.</param>
/// <param name="FailOnError">When true, any failed asset download raises an exception during finalize.</param>
/// <param name="CacheDirectory">UTF-8 absolute path to the on-disk cache that survives <c>clean</c> builds. Empty falls back to <c>{outputRoot}/.cache/privacy</c>.</param>
/// <param name="MaxRetries">Maximum retry attempts per asset on transient HTTP failures.</param>
/// <param name="UrlIncludePatterns">UTF-8 URL-level glob patterns that broaden the allow set beyond <see cref="HostsAllowed"/>.</param>
/// <param name="UrlExcludePatterns">UTF-8 URL-level glob patterns that drop matched URLs even when the host would otherwise pass.</param>
/// <param name="GenerateCspManifest">When true, the plugin computes SHA-256 hashes of every inline <c>&lt;style&gt;</c>/<c>&lt;script&gt;</c> body.</param>
/// <param name="CspManifestPath">UTF-8 forward-slash relative path under the output root where the CSP-hash manifest is written.</param>
public readonly record struct PrivacyOptions(
    bool Enabled,
    byte[] AssetDirectory,
    int DownloadParallelism,
    TimeSpan DownloadTimeout,
    byte[][] HostsToSkip,
    byte[][] HostsAllowed,
    bool AuditOnly,
    byte[] AuditManifestPath,
    bool AddRelNoOpener,
    bool AddTargetBlank,
    bool UpgradeMixedContent,
    bool FailOnError,
    byte[] CacheDirectory,
    int MaxRetries,
    byte[][] UrlIncludePatterns,
    byte[][] UrlExcludePatterns,
    bool GenerateCspManifest,
    byte[] CspManifestPath)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static PrivacyOptions Default { get; } = new(
        Enabled: true,
        AssetDirectory: [.. "assets/external"u8],
        DownloadParallelism: 4,
        DownloadTimeout: TimeSpan.FromSeconds(10),
        HostsToSkip: [],
        HostsAllowed: [],
        AuditOnly: false,
        AuditManifestPath: [.. "privacy-audit.json"u8],
        AddRelNoOpener: true,
        AddTargetBlank: false,
        UpgradeMixedContent: true,
        FailOnError: false,
        CacheDirectory: [],
        MaxRetries: 3,
        UrlIncludePatterns: [],
        UrlExcludePatterns: [],
        GenerateCspManifest: false,
        CspManifestPath: [.. "csp-hashes.json"u8]);
}
