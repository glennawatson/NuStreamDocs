# Privacy, Content Security Policy & Self-Hosted Fonts

**Packages**: `NuStreamDocs.Privacy`, `NuStreamDocs.Csp`, `NuStreamDocs.Fonts`

## Zero-Third-Party-Leak Stack

Combine all three plugins so visitors never make runtime requests to external CDNs or font servers:

```csharp
using NuStreamDocs.Csp;
using NuStreamDocs.Fonts;
using NuStreamDocs.Privacy;

builder
    .UseFonts(Material3Fonts.Default)
    .UsePrivacy(opts => opts
        .WithUpgradeMixedContent(true)
        .WithAddRelNoOpener(true))
    .UseCsp(opts => opts
        .WithHashInlineScripts(true)
        .WithUpgradeInsecureRequests(true));
```

## `NuStreamDocs.Fonts`

Downloads declared `.woff2` font files at build time (with an on-disk cache and `Offline: true` CI lock mode), writes `@font-face` declarations with metric-matched fallback `size-adjust` / `ascent-override` rules to eliminate layout shift, and injects `<link rel="preload" as="font" type="font/woff2" crossorigin>`.

## `NuStreamDocs.Privacy`

Scans rendered HTML for external `<img src>`, `<script src>`, and `<link href>` URLs, downloads allowed third-party assets into `assets/external/`, rewrites HTML references to the local copies, upgrades `http://` to `https://`, and adds `rel="noopener noreferrer"` to external links. Interactive video player hosts (`youtube-nocookie.com`, `player.vimeo.com`, etc.) are skipped automatically.

## `NuStreamDocs.Csp`

Runs after `PrivacyPlugin` (`PluginBand.Latest`), computes SHA-256 hashes of every inline `<script>` (and optionally `<style>`) on each page, and injects a per-page `<meta http-equiv="Content-Security-Policy">` tag locked down to `'self'` plus the exact inline hashes. Supports `CspMode.Enforce` and `CspMode.ReportOnly`.
