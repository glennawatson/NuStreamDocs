# Git Revision Dates & Contributors

**Package**: `NuStreamDocs.GitInfo`

## What It Does

`NuStreamDocs.GitInfo` reads the Git commit history of your documentation folder in **one batched `git log` pass** at configure time (zero per-page `git` subprocesses) and enriches your rendered site in three places:

1. **Page Revision Footer (`<footer class="nd-git-info">`)**: Appends the page's most recent ISO-8601 revision date (`Last updated <time datetime="YYYY-MM-DD">YYYY-MM-DD</time>`) and distinct contributor names (`Contributors: Alice, Bob`) inside `<article>`.
2. **HTML `<head>` Metadata**: Injects `<meta name="dcterms.modified" content="YYYY-MM-DD">` before `</head>`.
3. **`sitemap.xml` `<lastmod>` Enrichment**: Runs at `PluginBand.Latest` during finalize to insert `<lastmod>YYYY-MM-DD</lastmod>` into matching `<url><loc>...</loc></url>` entries written by `NuStreamDocs.Sitemap`.

## Installation

```bash
dotnet add package NuStreamDocs.GitInfo
```

## Quick Start

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.GitInfo;
using NuStreamDocs.Sitemap;
using NuStreamDocs.Theme.Material3;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteUrl("https://docs.example.com"u8)
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseSitemap()
    .UseGitInfo(opts => opts
        .WithMaxContributors(5)
        .WithEnrichSitemap(true))
    .BuildAsync();
```

## Emitted Markup Example

For a page modified on `2026-11-28` by `Alice` and earlier by `Bob`:

**Injected into `<head>`:**
```html
<link rel="stylesheet" href="/assets/extensions/git-info.css">
<meta name="dcterms.modified" content="2026-11-28">
```

**Injected before `</article>`:**
```html
<footer class="nd-git-info">
  <span class="nd-git-info__date">Last updated <time datetime="2026-11-28">2026-11-28</time></span>
  <span class="nd-git-info__authors">Contributors: Alice, Bob</span>
</footer>
```

**Updated in `site/sitemap.xml`:**
```xml
<url><loc>https://docs.example.com/guide/intro/</loc><lastmod>2026-11-28</lastmod></url>
```

## Frontmatter Overrides and Opt-Out

Override the revision date or lead author on synthetic or imported pages, or opt a page out completely:

```yaml
---
title: Imported Specification
last_updated: 2026-11-15   # fallback alias: date: 2026-11-15
author: Specification Team
---
```

Suppress Git metadata on a specific page:

```yaml
---
title: Landing Page
git_info: false
---
```

## Configuration Options (`GitInfoOptions`)

| Option | Fluent Method | Default | Description |
|---|---|---|---|
| `EmitFooter` | `.WithEmitFooter(bool)` | `true` | Appends `<footer class="nd-git-info">` inside rendered pages. |
| `EmitContributors` | `.WithEmitContributors(bool)` | `true` | Displays distinct contributor names alongside the revision date. |
| `EmitMetaTag` | `.WithEmitMetaTag(bool)` | `true` | Injects `<meta name="dcterms.modified">` into `<head>`. |
| `EnrichSitemap` | `.WithEnrichSitemap(bool)` | `true` | Inserts `<lastmod>` tags into `sitemap.xml` when `SiteUrl` and `sitemap.xml` exist. |
| `IncludeStylesheet` | `.WithIncludeStylesheet(bool)` | `true` | Emits and links `assets/extensions/git-info.css`. |
| `MaxContributors` | `.WithMaxContributors(int)` | `5` | Maximum number of distinct contributors shown per page. |
| `LogProvider` | `.WithLogProvider(IGitLogProvider)` | `null` | Custom `IGitLogProvider` implementation (`null` uses `ProcessGitLogProvider.Instance`). |
