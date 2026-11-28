# Sitemap, 404 & Redirects

**Packages**: `NuStreamDocs.Sitemap`, `NuStreamDocs.Redirects`

## `NuStreamDocs.Sitemap`

Provides site-level discovery and fallback pages:

- **`.UseSitemap()`**: Collects every rendered page URL and writes `sitemap.xml` plus `robots.txt` (with `Sitemap: <site_url>/sitemap.xml`) when `SiteUrl` is configured on `DocBuilder`.
- **`.UseNotFoundPage()`**: Emits a themed `404.html` page at the output root when the input directory does not supply a custom `404.md`.

```csharp
using NuStreamDocs.Sitemap;

builder
    .WithSiteUrl("https://docs.example.com"u8)
    .UseSitemap()
    .UseNotFoundPage();
```

---

## `NuStreamDocs.Redirects`

Prevents broken URLs when reorganizing documentation by generating:

1. A Netlify / Cloudflare Pages `_redirects` file at the site root.
2. Static `<meta http-equiv="refresh">` HTML stub pages at each old path (so GitHub Pages, S3, and Azure Static Web Apps redirect without server rules).
3. A `_headers` file with long-lived immutable cache headers for `/assets/fonts/*` and `/assets/*`.

### Frontmatter `redirect_from` & Explicit Rules

In any moved Markdown file:

```yaml
---
title: New Configuration Guide
redirect_from:
  - /old/config.html
  - /setup/options/
---
```

And in `Program.cs`:

```csharp
using NuStreamDocs.Redirects;

builder.UseRedirects(opts => opts
    .AddRedirect("/legacy-home"u8, "/getting-started/"u8)
    .WithDefaultCacheHeaders(true));
```
