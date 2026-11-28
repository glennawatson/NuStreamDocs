# Social Cards, OpenGraph & JSON-LD (`NuStreamDocs.Social`)

`NuStreamDocs.Social` adds rich social-sharing previews and search-engine structured data to every page of your documentation site in pure UTF-8 with zero external image libraries or headless browsers:

- **OpenGraph (`og:*`)**: `og:type`, `og:title`, `og:description`, `og:site_name`, `og:url`, and `og:image` (with `1200x630` SVG dimensions).
- **Twitter / X Cards (`twitter:*`)**: `twitter:card` (`summary_large_image` or `summary`), `twitter:title`, `twitter:description`, `twitter:site`, and `twitter:image`.
- **Canonical URLs**: `<link rel="canonical" href="...">` when `WithSiteUrl(...)` is configured and no canonical tag is already in `<head>`.
- **Schema.org Structured Data**: `<script type="application/ld+json">` emitting a `TechArticle` object (`headline`, `description`, `url`, `image`, `author`, and `publisher`).
- **Deterministic SVG Preview Cards**: Writes crisp `1200x630` branded SVG cards to `assets/social/<slug>.svg` for any page that does not set an explicit frontmatter `image:` override.

Because `SocialPlugin` runs before `CspPlugin` (`PluginBand.Latest` tiebreak `40` vs `100`), its inline JSON-LD `<script>` block is automatically SHA-256 hashed when `NuStreamDocs.Csp` is enabled.

## Installation

```bash
dotnet add package NuStreamDocs.Social
```

## Quick Start

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Social;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteName("My Library"u8)
    .WithSiteAuthor("Jane Doe"u8)
    .WithSiteUrl("https://docs.example.com"u8)
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseSocial(opts => opts
        .WithTwitterSite("@mylibrary"u8)
        .WithCardColors("#0f172a"u8, "#38bdf8"u8))
    .BuildAsync();
```

## How Metadata Is Resolved Per Page

For every Markdown page, `NuStreamDocs.Social` resolves metadata in priority order:

1. **Frontmatter keys** (`---` YAML header):
   - `title`: Page title.
   - `description`: Summary text.
   - `social_image` or `image`: Custom preview image URL or site-relative path (skips SVG generation for that page).
2. **Markdown body fallbacks**:
   - If `title` is omitted, the first `# Heading` outside fenced code blocks is used, falling back to the humanized filename (or `SiteName` / `"Home"` on `index.md`).
   - If `description` is omitted, the first plain prose line outside code fences, headings, lists, blockquotes, tables, and HTML blocks is cleaned and truncated at 160 UTF-8 bytes.

### Example Frontmatter Override

```markdown
---
title: Getting Started with Streaming
description: Install the NuGet packages and build your first site in 30 seconds.
social_image: /images/getting-started-banner.png
---

# Getting Started
```

### Generated `<head>` Output

For `guide/setup.md` without an `image` override, `NuStreamDocs.Social` emits `site/assets/social/guide-setup.svg` and injects:

```html
<link rel="canonical" href="https://docs.example.com/guide/setup/">
<meta property="og:type" content="article">
<meta property="og:title" content="Setup">
<meta property="og:description" content="Configure your project in three steps.">
<meta property="og:site_name" content="My Library">
<meta property="og:url" content="https://docs.example.com/guide/setup/">
<meta property="og:image" content="https://docs.example.com/assets/social/guide-setup.svg">
<meta property="og:image:type" content="image/svg+xml">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="Setup">
<meta name="twitter:description" content="Configure your project in three steps.">
<meta name="twitter:site" content="@mylibrary">
<meta name="twitter:image" content="https://docs.example.com/assets/social/guide-setup.svg">
<script type="application/ld+json">{"@context":"https://schema.org","@type":"TechArticle","headline":"Setup",...}</script>
```

## Options Reference (`SocialOptions`)

| Property / Fluent Helper | Default | Purpose |
|---|---|---|
| `Enabled` / `.Disable()` | `true` | Master toggle for head injection and SVG card emission. |
| `EmitOpenGraph` / `.WithoutOpenGraph()` | `true` | Emits `<meta property="og:*">` tags. |
| `EmitTwitterCard` / `.WithoutTwitterCard()` | `true` | Emits `<meta name="twitter:*">` tags. |
| `EmitJsonLd` / `.WithoutJsonLd()` | `true` | Emits Schema.org `TechArticle` `<script type="application/ld+json">`. |
| `EmitCanonicalLink` / `.WithoutCanonicalLink()` | `true` | Emits `<link rel="canonical">` when `SiteUrl` is configured. |
| `GenerateSvgCards` / `.WithoutSvgCards()` | `true` | Writes `1200x630` SVG preview cards to `assets/social/<slug>.svg`. |
| `TwitterSite` / `.WithTwitterSite(handle)` | `[]` | Optional `@handle` emitted in `twitter:site`. |
| `BackgroundColor`, `AccentColor` / `.WithCardColors(bg, accent)` | `"#0f172a"`, `"#2563eb"` | Canvas background and accent bar colors on generated SVG cards. |
