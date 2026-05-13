[![CI Build](https://github.com/glennawatson/NuStreamDocs/actions/workflows/ci.yml/badge.svg)](https://github.com/glennawatson/NuStreamDocs/actions/workflows/ci.yml)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_NuStreamDocs&metric=coverage)](https://sonarcloud.io/summary/new_code?id=glennawatson_NuStreamDocs)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_NuStreamDocs&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_NuStreamDocs)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_NuStreamDocs&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=glennawatson_NuStreamDocs)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_NuStreamDocs&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=glennawatson_NuStreamDocs)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=glennawatson_NuStreamDocs&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=glennawatson_NuStreamDocs)
[![NuGet](https://img.shields.io/nuget/v/NuStreamDocs.svg?logo=nuget&label=NuStreamDocs)](https://www.nuget.org/packages/NuStreamDocs/)
[![Downloads](https://img.shields.io/nuget/dt/NuStreamDocs.svg?logo=nuget&label=downloads)](https://www.nuget.org/packages/NuStreamDocs/)
[![GitHub stars](https://img.shields.io/github/stars/glennawatson/NuStreamDocs?style=social)](https://github.com/glennawatson/NuStreamDocs/stargazers)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br>
<a href="https://github.com/glennawatson/NuStreamDocs">
  <img width="160" height="160" src="https://raw.githubusercontent.com/glennawatson/NuStreamDocs/main/icons/nustreamdocs.png" alt="NuStreamDocs">
</a>
<br>

# NuStreamDocs

A high-performance, AOT-friendly **library** for building static documentation
sites from Markdown. You write a tiny `Program.cs`, choose your theme and
plugins via a fluent `DocBuilder`, and `dotnet run` produces the site.

There's **no CLI to install**. You wire the generator into your own console
project, exactly like [Statiq](https://www.statiq.dev/). That gives you full
intellisense over plugin options, type-safe configuration, and the ability to
drop in custom plugins by adding one `.UsePlugin(myPlugin)` line.

The default look targets parity with
[mkdocs-material](https://github.com/squidfunk/mkdocs-material) /
[Zensical](https://github.com/zensical/zensical). Theme assets ship as
embedded resources so a published build is single-file and works under
`dotnet publish -p:PublishAot=true`.

---

## Table of contents

- [Quick start](#quick-start)
- [The builder](#the-builder)
- [Themes](#themes)
- [Plugins (overview)](#plugins-overview)
- [Markdown extensions](#markdown-extensions)
- [Recipes](#recipes)
- [Configuration files (mkdocs.yml / Zensical TOML)](#configuration-files-mkdocsyml--zensical-toml)
- [Module reference](#module-reference)
- [Performance and AOT](#performance-and-aot)
- [Testing](#testing)
- [Contributing](#contributing)

---

## Quick start

### 1. Create a docs project

```bash
mkdir mydocs && cd mydocs
dotnet new console
```

### 2. Add the packages you need

At minimum you need the core plus a theme. Most sites also want nav, search,
highlight, and a TOC.

```bash
dotnet add package NuStreamDocs
dotnet add package NuStreamDocs.Theme.Material        # or .Theme.Material3
dotnet add package NuStreamDocs.Nav
dotnet add package NuStreamDocs.Search
dotnet add package NuStreamDocs.Highlight
dotnet add package NuStreamDocs.Toc
dotnet add package NuStreamDocs.MarkdownExtensions    # admonitions, tabs, footnotes…
```

### 3. Write `Program.cs`

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Highlight;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Nav;
using NuStreamDocs.Search;
using NuStreamDocs.Theme.Material;
using NuStreamDocs.Toc;

var pages = await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme()
    .UseNav(opts => opts with { Prune = true })
    .UseToc()
    .UseHighlight()
    .UseSearch()
    .UseCommonMarkdownExtensions()
    .BuildAsync();

Console.WriteLine($"Built {pages} page(s).");
```

### 4. Drop your Markdown under `docs/`

```
mydocs/
  Program.cs
  docs/
    index.md
    guide/
      getting-started.md
      configuration.md
```

### 5. Build and view

```bash
dotnet run
# then serve site/ with any static server, e.g.
dotnet tool install -g dotnet-serve
dotnet serve -d site
```

### Optional: AOT publish

```bash
dotnet publish -c Release -p:PublishAot=true
./bin/Release/net10.0/<rid>/publish/mydocs
```

The whole pipeline runs without reflection in the hot path, so the AOT-published
binary starts and renders immediately.

---

## The builder

`DocBuilder` is the canonical surface. Everything else is an extension method on
top of it.

```csharp
new DocBuilder()
    .WithLogger(myLogger)            // Microsoft.Extensions.Logging ILogger
    .WithInput("docs")               // source root
    .WithOutput("site")              // output root
    .UseDirectoryUrls()              // /guide/intro/ instead of /guide/intro.html
    .IncludeDrafts(false)            // skip *.draft.md
    .Include("**/*.md")              // glob filters layered over input
    .Exclude("**/_*.md")
    .UseMaterialTheme()              // pick a theme (required)
    .UseNav()                        // any number of plugins
    .UseSearch()
    // ...
    .BuildAsync(cancellationToken);  // returns Task<int> = pages built
```

### Key facts about plugin registration

- **`.UsePlugin<TPlugin>()`** — the AOT seam. `TPlugin : IDocPlugin, new()`,
  so registration is a direct constructor call. No reflection, no
  `Activator.CreateInstance`.
- **`.UsePlugin(plugin)`** — when you want to pass a pre-configured instance
  (with options, logger, dependencies). All of the convenience `Use*`
  extension methods route through this.
- **Order is preserved**, but each plugin declares its own pipeline phase
  (`OnConfigure` / `OnRenderPage` / `OnFinalize`), so most ordering is
  automatic.
- **Plugins are pluggable units**, not magical reflection scans. If you want
  a custom plugin, write one `IDocPlugin` and `.UsePlugin(myPlugin)`.

---

## Themes

Pick exactly one theme assembly:

| Package                        | What                                                                                                                                                                      |
|--------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `NuStreamDocs.Theme.Material`  | Classic mkdocs-material look. Mustache page template + Material CSS/JS bundle, all embedded.                                                                              |
| `NuStreamDocs.Theme.Material3` | Material Design 3 — design-token-driven (color roles, shape, typography, elevation), structurally inspired by mkdocs-material 9.x but rebuilt for the modern MD3 surface. |

```csharp
.UseMaterialTheme()                                            // defaults
.UseMaterialTheme(opts => opts with { AssetSource = ... })     // CDN vs embedded
.UseMaterial3Theme()
```

Both themes honor the same `IStaticAssetProvider` /
`IHeadExtraProvider` contracts, so any plugin written once works under either
theme without recompilation.

### Configuring with UTF-8 string literals (`"..."u8`)

The fluent option setters that take user-facing text or URLs accept **UTF-8
string literals** (`"..."u8`) directly — same shape the engine uses
internally, so the value lands as bytes with no per-call UTF-8 encode:

```csharp
.UseMaterial3Theme(opts => opts
    .WithSiteName("My Project"u8)
    .WithLogo("images/logo.png"u8)
    .WithSiteUrl("https://example.com"u8)
    .WithCopyright("Copyright © 2026 My Project Authors"u8)
    .WithRepoUrl("https://github.com/me/my-project"u8)
    .WithEditUri("https://github.com/me/my-project/edit/main/docs/"u8))
```

The same pattern applies wherever a plugin's option setters take inline
strings — for example `.UsePrivacy(opts => opts.AddHostsToSkip("example.com"u8))`.
Use `"..."u8` for literal values and the regular `string` overloads for
values you've already loaded from configuration.

---

## Plugins (overview)

Every plugin ships in its own NuGet so you only pay for what you use. Each row
links to the package on NuGet — the badge tracks the current published version.

### Essentials (most sites want all of these)

| Package                                      | NuGet                                 | Builder                                    | What                                                                                                                                                                                                                                                                                                        |
|----------------------------------------------|---------------------------------------|--------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Nav`][Nav]                    | [![ver][NavV]][Nav]                   | `.UseNav()`                                | Glob includes, ordering hints, hidden sections, `.pages` overrides, `navigation.prune`, orphan-page warnings.                                                                                                                                                                                               |
| [`NuStreamDocs.Toc`][Toc]                    | [![ver][TocV]][Toc]                   | `.UseToc()`                                | Per-page table of contents and permalink heading anchors.                                                                                                                                                                                                                                                   |
| [`NuStreamDocs.Highlight`][Highlight]        | [![ver][HighlightV]][Highlight]       | `.UseHighlight()`                          | Server-side syntax highlighting. TextMate JSON grammars + `[GeneratedRegex]`. Wraps blocks in `<div class="highlight">` (Pygments / mkdocs-material convention); reads per-block fence-info attrs (`title="..."` for filename bar, opt-in copy button). No JS runtime.                                      |
| [`NuStreamDocs.Search`][Search]              | [![ver][SearchV]][Search]             | (base; pair with one of the engines below) | Shared `SearchPluginBase`, head-extra wiring, section-priority meta.                                                                                                                                                                                                                                        |
| [`NuStreamDocs.Search.Pagefind`][Pagefind]   | [![ver][PagefindV]][Pagefind]         | `.UsePagefindSearch()`                     | Pagefind WASM index with snippets. Ships per-RID native binary; runs the CLI at finalize.                                                                                                                                                                                                                   |
| [`NuStreamDocs.Search.Lunr`][Lunr]           | [![ver][LunrV]][Lunr]                 | `.UseLunrSearch()`                         | Lunr-compatible JSON index. Pure-JS runtime, no native binary.                                                                                                                                                                                                                                              |
| [`NuStreamDocs.Search.Sqlite`][SqliteSearch] | [![ver][SqliteSearchV]][SqliteSearch] | `.UseSqliteSearch()`                       | One `search.db` (SQLite/FTS5), queried client-side via `sql.js-httpvfs` over HTTP range requests. Pick this over Pagefind when the site is large enough that Pagefind's per-page fragment files would blow a static host's file-count limit (e.g. a multi-thousand-page API reference on Cloudflare Pages). |
| [`NuStreamDocs.MarkdownExtensions`][MdExt]   | [![ver][MdExtV]][MdExt]               | `.UseCommonMarkdownExtensions()`           | Admonitions, content tabs, collapsible details, check-lists, mark spans, footnotes, definition lists, **abbreviations** (`*[…]: definition`), tables, attr-list, mark, caret/tilde, critic-markup, inline-hilite, markdown-in-html.                                                                         |

### Authoring helpers

| Package                            | NuGet                     | Builder              | What                                                                                                                                                                                                                                                                                                                                                                                                            |
|------------------------------------|---------------------------|----------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Macros`][Macros]    | [![ver][MacrosV]][Macros] | `.UseMacros()`       | Variable substitution. `{{ name }}` markers in markdown resolve through a host-supplied `Dictionary<string, string>`. Skips fenced + inline code regions. Closes the mkdocs-macros gap (variable substitution slice).                                                                                                                                                                                           |
| [`NuStreamDocs.Bibliography`][Bib] | [![ver][BibV]][Bib]       | `.UseBibliography()` | Pandoc-style citations — `[@key]` / `[@key, p 23]` / `[@a; @b]` markers resolve through a `BibliographyDatabase` (fluent builder, CSL-JSON loader). Emits a numbered footnote per citation plus a bibliography section. Ships **AGLC4** (Australian Guide to Legal Citation, 4th ed) as the v1 style; CSL-aware data model so other styles slot in. Byte-level UTF-8, zero-string-alloc on the format hot path. |

### Markdown syntax extensions (à la carte)

Each is a separate assembly so you only pull what you use:

| Package                                      | NuGet                       | Builder              | Syntax                                                                                                                                                                                                                                                            |
|----------------------------------------------|-----------------------------|----------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Arithmatex`][Arith]           | [![ver][ArithV]][Arith]     | `.UseArithmatex()`   | `$x$` / `$$x$$` math (MathJax/KaTeX-friendly markup)                                                                                                                                                                                                              |
| [`NuStreamDocs.Arithmatex.MathJax`][ArithMJ] | [![ver][ArithMJV]][ArithMJ] | `.UseMathJax()`      | Pairs with Arithmatex; injects MathJax 3 client-side runtime                                                                                                                                                                                                      |
| [`NuStreamDocs.Emoji`][Emoji]                | [![ver][EmojiV]][Emoji]     | `.UseEmoji()`        | `:name:` → twemoji `<span>`                                                                                                                                                                                                                                       |
| [`NuStreamDocs.Keys`][Keys]                  | [![ver][KeysV]][Keys]       | `.UseKeys()`         | `++ctrl+alt+del++` → keyboard `<span>`                                                                                                                                                                                                                            |
| [`NuStreamDocs.MagicLink`][Magic]            | [![ver][MagicV]][Magic]     | `.UseMagicLink()`    | Bare URLs become autolinks                                                                                                                                                                                                                                        |
| [`NuStreamDocs.SmartSymbols`][Smart]         | [![ver][SmartV]][Smart]     | `.UseSmartSymbols()` | © ® ™, c/o, ±, ≠, → …                                                                                                                                                                                                                                             |
| [`NuStreamDocs.Snippets`][Snip]              | [![ver][SnipV]][Snip]       | `.UseSnippets()`     | Whole-file (`--8<-- "file"`) and section (`--8<-- "file#name"`) includes. Section boundaries inside snippet files use HTML comments — `<!-- @section name -->` / `<!-- @endsection -->` — invisible in any CommonMark renderer even when the plugin isn't loaded. |
| [`NuStreamDocs.SuperFences`][SF]             | [![ver][SFV]][SF]           | `.UseSuperFences()`  | Custom-fence dispatcher (auto-discovers fence handlers)                                                                                                                                                                                                           |

> Abbreviations (`*[token]: definition` → `<abbr>`) ship as part of
> `NuStreamDocs.MarkdownExtensions` — `.UseCommonMarkdownExtensions()` enables
> them by default, or `.UseAbbreviations()` for a standalone opt-in.

### Site emitters

| Package                                  | NuGet                       | Builder                                                  | What                                                                                                                                                                                                                           |
|------------------------------------------|-----------------------------|----------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Sitemap`][Site]           | [![ver][SiteV]][Site]       | `.UseSitemap()`, `.UseNotFoundPage()`, `.UseRedirects()` | sitemap.xml + robots.txt, default 404, redirect stubs                                                                                                                                                                          |
| [`NuStreamDocs.Versions`][Ver]           | [![ver][VerV]][Ver]         | `.UseVersions()`                                         | mike-equivalent versioning manifest (`versions.json`)                                                                                                                                                                          |
| [`NuStreamDocs.Tags`][Tags]              | [![ver][TagsV]][Tags]       | `.UseTags()`                                             | Tag index + per-tag listing pages from `tags:` frontmatter                                                                                                                                                                     |
| [`NuStreamDocs.Metadata`][Meta]          | [![ver][MetaV]][Meta]       | `.UseMetadata()`                                         | Directory-level (`_meta.yml`) + sidecar (`page.meta.yml`) frontmatter merging (Statiq-inspired)                                                                                                                                |
| [`NuStreamDocs.Autorefs`][Auto]          | [![ver][AutoV]][Auto]       | `.UseAutorefs()`                                         | `@autoref:ID` rewriting against collected heading anchors                                                                                                                                                                      |
| [`NuStreamDocs.Xrefs`][Xref]             | [![ver][XrefV]][Xref]       | `.UseXrefs()`                                            | DocFX-style `xrefmap.json` emit + import                                                                                                                                                                                       |
| [`NuStreamDocs.SphinxInventory`][Sphinx] | [![ver][SphinxV]][Sphinx]   | `.UseSphinxInventory()`                                  | Sphinx-compatible `objects.inv` emitter. Snapshots the shared autorefs registry at finalize time and writes a v2 inventory file (zlib-compressed body) so external Sphinx sites can intersphinx-link into NuStreamDocs builds. |
| [`NuStreamDocs.Layouts`][Layouts]        | [![ver][LayoutsV]][Layouts] | `.UseLayouts()`                                          | Jinja-style `{% extends %}` / `{% block %}` template inheritance for shared page layouts.                                                                                                                                      |

### Quality / validation

| Package                            | NuGet             | Builder               | What                                                                                                                                                                 |
|------------------------------------|-------------------|-----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.LinkValidator`][LV] | [![ver][LVV]][LV] | `.UseLinkValidator()` | Strict-mode link checker. Internal: relative + anchors + nav/disk consistency (mkdocs `--strict`). External: HTTP HEAD via Polly with host-batched throttling/retry. |

### Dev experience

| Package                       | NuGet                   | Builder                 | What                                                                                                                                                                                                 |
|-------------------------------|-------------------------|-------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Serve`][Serve] | [![ver][ServeV]][Serve] | `.WatchAndServeAsync()` | Long-running watch + dev-server loop. FileSystemWatcher with debounced rebuild + Kestrel-hosted static file server + LiveReload websocket so connected browsers refresh on every successful rebuild. |

### Blog & syndication

| Package                              | NuGet                     | Builder            | What                                                                                                      |
|--------------------------------------|---------------------------|--------------------|-----------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Blog`][Blog]          | [![ver][BlogV]][Blog]     | `.UseWyamBlog()`   | Wyam-flavored blog: `YYYY-MM-DD-slug.md` + Wyam frontmatter (NoTitle/IsBlog/Title/Tags/Author/Published). |
| [`NuStreamDocs.Blog.MkDocs`][BlogMk] | [![ver][BlogMkV]][BlogMk] | `.UseMkDocsBlog()` | mkdocs-material flavored: posts under `blog/posts/` with categories/date/authors frontmatter.             |
| [`NuStreamDocs.Feed`][Feed]          | [![ver][FeedV]][Feed]     | `.UseFeed()`       | RSS 2.0 / Atom feed generation off the same blog scanner.                                                 |

### Content sources (pull pages from outside the input folder)

| Package                                       | NuGet                     | Builder                                                                | What                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
|-----------------------------------------------|---------------------------|------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.ContentLoader`][CL]            | [![ver][CLV]][CL]         | `.UseContentLoaders(...)`                                              | Lets pages come from somewhere other than local Markdown. An `IContentLoader` emits in-memory synthetic pages that flow through the normal render pipeline. Built-ins: `FileContentLoader` (a local JSON/YAML collection → one page per object, field-mapped: route template, body key, frontmatter); `HttpContentLoader` (a JSON HTTP endpoint, GET or POST-with-body for GraphQL); `RawDocumentContentLoader` (a fixed list of raw remote Markdown URLs, body used verbatim — works for GitHub/GitLab/Gitea/CDN raw links). |
| [`NuStreamDocs.ContentLoader.Feed`][CLFeed]   | [![ver][CLFeedV]][CLFeed] | `new FeedContentLoader(...)`                                           | Pulls an external RSS/Atom feed and turns each item into a page (title/date/source/external_url frontmatter, item content as the body) — the consume-side counterpart to `NuStreamDocs.Feed`.                                                                                                                                                                                                                                                                                                                                 |
| [`NuStreamDocs.ContentLoader.GitHub`][CLGH]   | [![ver][CLGHV]][CLGH]     | `new GitHubContentLoader(...)`, `new GitHubReleasesContentLoader(...)` | Pull the Markdown under a path in a GitHub repo at any branch / tag / commit (conceptual docs living next to the product code), or turn a repo's releases into changelog pages. Uses the GitHub REST API directly — no Octokit; optional token for private repos / rate limits.                                                                                                                                                                                                                                               |
| [`NuStreamDocs.ContentLoader.OpenApi`][CLOAS] | [![ver][CLOASV]][CLOAS]   | `new OpenApiContentLoader(...)`                                        | Turns an OpenAPI 3.x spec (JSON or YAML, local file or URL) into reference pages — one page per tag, each operation rendered with its parameters table, request body, and responses.                                                                                                                                                                                                                                                                                                                                          |

### Media

| Package                                     | NuGet                 | Builder                 | What                                                                                                                                                                                                                                                                                                                                                                                                                                     |
|---------------------------------------------|-----------------------|-------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Mermaid`][Mer]               | [![ver][MerV]][Mer]   | `.UseMermaid()`         | Retag fenced `mermaid` blocks for the Mermaid runtime.                                                                                                                                                                                                                                                                                                                                                                                   |
| [`NuStreamDocs.Lightbox`][LB]               | [![ver][LBV]][LB]     | `.UseLightbox()`        | glightbox: wraps content images in lightbox triggers, ships glightbox CSS/JS.                                                                                                                                                                                                                                                                                                                                                            |
| [`NuStreamDocs.Icons.Material`][IcM]        | [![ver][IcMV]][IcM]   | `.UseMaterialIcons()`   | Google Material Icons / Material Symbols (Outlined/Rounded/Sharp) — emits the stylesheet `<link>` so font-ligature spans render.                                                                                                                                                                                                                                                                                                         |
| [`NuStreamDocs.Icons.MaterialDesign`][IcMD] | [![ver][IcMDV]][IcMD] | `new MdiIconResolver()` | **Inline-SVG** Material Design Icons (Pictogrammers MDI, ~7,400 icons). Plugs into the icon-shortcode rewriter as an `IIconResolver` so `:material-foo:` shortcodes inline the actual SVG path data — matches what mkdocs-material emits and works for the much larger MDI namespace (which Google Material Symbols doesn't fully cover). Path data is baked into a generated bucket-by-length switch; zero startup cost, ~50 ns lookup. |
| [`NuStreamDocs.Icons.FontAwesome`][IcFA]    | [![ver][IcFAV]][IcFA] | `.UseFontAwesome()`     | Font Awesome Free from a configurable CDN.                                                                                                                                                                                                                                                                                                                                                                                               |

### Privacy & optimization

| Package                             | NuGet                   | Builder                              | What                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
|-------------------------------------|-------------------------|--------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Privacy`][Priv]      | [![ver][PrivV]][Priv]   | `.UsePrivacy()`                      | Localizes external assets under `assets/external/` and rewrites HTML to point at the local copies. Byte-level UTF-8 throughout.                                                                                                                                                                                                                                                                                                                             |
| [`NuStreamDocs.Csp`][Csp]           | [![ver][CspV]][Csp]     | `.UseCsp(...)`                       | Per-page `Content-Security-Policy`: hashes each page's inline scripts (and, optionally, styles) and injects a `<meta http-equiv="Content-Security-Policy">` with a `'self'`-based directive set tuned for self-hosted static sites (`object-src 'none'`, `img-src 'self' data:`, hashed `script-src`, …). Report-only mode, `report-uri`, and per-directive source allow-listing supported. Pairs with `UsePrivacy()` (runs after it).                      |
| [`NuStreamDocs.Fonts`][Fonts]       | [![ver][FontsV]][Fonts] | `.UseFonts(...)`                     | Self-hosts declared fonts (Google Fonts, Fontsource, or local files): downloads the woff2 at build time, generates `@font-face` + `<link rel="preload">`, and emits a system-font fallback with `size-adjust` / `ascent-override` so the swap to the webfont causes zero layout shift. No third-party requests at runtime. Theme presets: `Material3Fonts.Default`, `MaterialFonts.Default`.                                                                |
| [`NuStreamDocs.Transitions`][Trans] | [![ver][TransV]][Trans] | `.UseTransitions(...)`               | Client-side routing + view transitions: ships a tiny vanilla-JS router that intercepts same-origin links, swaps the content region in place (animated via the browser's View Transitions API), keeps the sidebar / search / scroll position, and pre-fetches links on hover so navigation feels instant. No JS framework; degrades to plain full-page navigation when JS or the View Transitions API is unavailable.                                        |
| [`NuStreamDocs.Redirects`][Redir]   | [![ver][RedirV]][Redir] | `.UseRedirects(...)`                 | Emits deploy-host config at build time: a Netlify/Cloudflare-Pages `_redirects` file and a meta-refresh HTML page for every declared redirect (so renamed/moved pages don't 404 on any static host), plus a `_headers` file with immutable-caching defaults for the content-hashed asset dirs. Redirects come from `UseRedirects(...)` config and a `redirect_from` key in page frontmatter.                                                                |
| [`NuStreamDocs.Audit`][Audit]       | [![ver][AuditV]][Audit] | `.UseAudit(...)`                     | Build-time accessibility / performance lints over the rendered site: images without `alt` or without `width`/`height`, skipped heading levels, zero/multiple `<h1>`, missing `<html lang>` / `<title>` / `<meta viewport>`, empty `<a>` / `<button>`, positive `tabindex`, unlabeled form controls, and render-blocking `<script src>` in `<head>`. Byte-level HTML scan; warns by default, fails the build (`exit 2`) under strict mode; per-rule opt-out. |
| [`NuStreamDocs.Optimize`][Opt]      | [![ver][OptV]][Opt]     | `.UseOptimize()`, `.UseHtmlMinify()` | Pre-compresses emitted output as `.gz` / `.br` siblings (truly-async .NET 10 stream APIs). HTML minify pass.                                                                                                                                                                                                                                                                                                                                                |

### C# API reference

| Package                                  | NuGet               | Builder                                                      | What                                                                                                                                                                                                                       |
|------------------------------------------|---------------------|--------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.CSharpApiGenerator`][Api] | [![ver][ApiV]][Api] | `.UseCSharpApiGenerator()`, `.UseCSharpApiGeneratorDirect()` | Wraps SourceDocParser + the Zensical emitter. Pulls NuGet-packaged assemblies (or DLLs / manifest / custom callbacks), generates Markdown reference pages into the docs tree, and lets normal page discovery pick them up. |

### Themes

| Package                               | NuGet               | Builder                | What                                                                                                                                                                      |
|---------------------------------------|---------------------|------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`NuStreamDocs.Theme.Material`][TM]   | [![ver][TMV]][TM]   | `.UseMaterialTheme()`  | Classic mkdocs-material look — Mustache page template + Material CSS/JS bundle, all embedded.                                                                             |
| [`NuStreamDocs.Theme.Material3`][TM3] | [![ver][TM3V]][TM3] | `.UseMaterial3Theme()` | Material Design 3 — design-token-driven (color roles, shape, typography, elevation), structurally inspired by mkdocs-material 9.x but rebuilt for the modern MD3 surface. |

### Core (no `.UseX()` — every plugin pulls these in transitively)

| Package                                | NuGet                 | What                                                                                    |
|----------------------------------------|-----------------------|-----------------------------------------------------------------------------------------|
| [`NuStreamDocs`][Core]                 | [![ver][CoreV]][Core] | Markdown parser, HTML emitter, page pipeline, plugin contract, `DocBuilder`.            |
| [`NuStreamDocs.Common`][Cm]            | [![ver][CmV]][Cm]     | Byte-level helpers (`AsciiByteHelpers`, path/URL structs, `Utf8Concat`, …).             |
| [`NuStreamDocs.Markdown.Common`][Mc]   | [![ver][McV]][Mc]     | Shared markdown scanners (`MarkdownCodeScanner`, `AsciiWordBoundary`, …).               |
| [`NuStreamDocs.Templating`][Tp]        | [![ver][TpV]][Tp]     | Span/UTF-8 Mustache-style template engine.                                              |
| [`NuStreamDocs.Theme.Common`][Tc]      | [![ver][TcV]][Tc]     | Shared theme helpers — icon-shortcode rewriter, embedded-asset loader, page-shell base. |
| [`NuStreamDocs.Config.MkDocs`][Cfg1]   | [![ver][Cfg1V]][Cfg1] | `mkdocs.yml` reader (span/UTF-8 YAML→JSON, no YamlDotNet).                              |
| [`NuStreamDocs.Config.Zensical`][Cfg2] | [![ver][Cfg2V]][Cfg2] | Zensical-flavored TOML config reader.                                                   |
| [`NuStreamDocs.Blog.Common`][Bc]       | [![ver][BcV]][Bc]     | Shared blog pipeline used by `Blog` and `Blog.MkDocs`.                                  |

[Core]: https://www.nuget.org/packages/NuStreamDocs/

[CoreV]: https://img.shields.io/nuget/v/NuStreamDocs.svg?label=

[Cm]: https://www.nuget.org/packages/NuStreamDocs.Common/

[CmV]: https://img.shields.io/nuget/v/NuStreamDocs.Common.svg?label=

[Mc]: https://www.nuget.org/packages/NuStreamDocs.Markdown.Common/

[McV]: https://img.shields.io/nuget/v/NuStreamDocs.Markdown.Common.svg?label=

[Tp]: https://www.nuget.org/packages/NuStreamDocs.Templating/

[TpV]: https://img.shields.io/nuget/v/NuStreamDocs.Templating.svg?label=

[Tc]: https://www.nuget.org/packages/NuStreamDocs.Theme.Common/

[TcV]: https://img.shields.io/nuget/v/NuStreamDocs.Theme.Common.svg?label=

[TM]: https://www.nuget.org/packages/NuStreamDocs.Theme.Material/

[TMV]: https://img.shields.io/nuget/v/NuStreamDocs.Theme.Material.svg?label=

[TM3]: https://www.nuget.org/packages/NuStreamDocs.Theme.Material3/

[TM3V]: https://img.shields.io/nuget/v/NuStreamDocs.Theme.Material3.svg?label=

[Cfg1]: https://www.nuget.org/packages/NuStreamDocs.Config.MkDocs/

[Cfg1V]: https://img.shields.io/nuget/v/NuStreamDocs.Config.MkDocs.svg?label=

[Cfg2]: https://www.nuget.org/packages/NuStreamDocs.Config.Zensical/

[Cfg2V]: https://img.shields.io/nuget/v/NuStreamDocs.Config.Zensical.svg?label=

[Nav]: https://www.nuget.org/packages/NuStreamDocs.Nav/

[NavV]: https://img.shields.io/nuget/v/NuStreamDocs.Nav.svg?label=

[Toc]: https://www.nuget.org/packages/NuStreamDocs.Toc/

[TocV]: https://img.shields.io/nuget/v/NuStreamDocs.Toc.svg?label=

[Highlight]: https://www.nuget.org/packages/NuStreamDocs.Highlight/

[HighlightV]: https://img.shields.io/nuget/v/NuStreamDocs.Highlight.svg?label=

[Search]: https://www.nuget.org/packages/NuStreamDocs.Search/

[SearchV]: https://img.shields.io/nuget/v/NuStreamDocs.Search.svg?label=

[Pagefind]: https://www.nuget.org/packages/NuStreamDocs.Search.Pagefind/

[PagefindV]: https://img.shields.io/nuget/v/NuStreamDocs.Search.Pagefind.svg?label=

[Lunr]: https://www.nuget.org/packages/NuStreamDocs.Search.Lunr/

[LunrV]: https://img.shields.io/nuget/v/NuStreamDocs.Search.Lunr.svg?label=

[SqliteSearch]: https://www.nuget.org/packages/NuStreamDocs.Search.Sqlite/

[SqliteSearchV]: https://img.shields.io/nuget/v/NuStreamDocs.Search.Sqlite.svg?label=

[MdExt]: https://www.nuget.org/packages/NuStreamDocs.MarkdownExtensions/

[MdExtV]: https://img.shields.io/nuget/v/NuStreamDocs.MarkdownExtensions.svg?label=

[Macros]: https://www.nuget.org/packages/NuStreamDocs.Macros/

[MacrosV]: https://img.shields.io/nuget/v/NuStreamDocs.Macros.svg?label=

[Bib]: https://www.nuget.org/packages/NuStreamDocs.Bibliography/

[BibV]: https://img.shields.io/nuget/v/NuStreamDocs.Bibliography.svg?label=

[Arith]: https://www.nuget.org/packages/NuStreamDocs.Arithmatex/

[ArithV]: https://img.shields.io/nuget/v/NuStreamDocs.Arithmatex.svg?label=

[ArithMJ]: https://www.nuget.org/packages/NuStreamDocs.Arithmatex.MathJax/

[ArithMJV]: https://img.shields.io/nuget/v/NuStreamDocs.Arithmatex.MathJax.svg?label=

[Emoji]: https://www.nuget.org/packages/NuStreamDocs.Emoji/

[EmojiV]: https://img.shields.io/nuget/v/NuStreamDocs.Emoji.svg?label=

[Keys]: https://www.nuget.org/packages/NuStreamDocs.Keys/

[KeysV]: https://img.shields.io/nuget/v/NuStreamDocs.Keys.svg?label=

[Magic]: https://www.nuget.org/packages/NuStreamDocs.MagicLink/

[MagicV]: https://img.shields.io/nuget/v/NuStreamDocs.MagicLink.svg?label=

[Smart]: https://www.nuget.org/packages/NuStreamDocs.SmartSymbols/

[SmartV]: https://img.shields.io/nuget/v/NuStreamDocs.SmartSymbols.svg?label=

[Snip]: https://www.nuget.org/packages/NuStreamDocs.Snippets/

[SnipV]: https://img.shields.io/nuget/v/NuStreamDocs.Snippets.svg?label=

[SF]: https://www.nuget.org/packages/NuStreamDocs.SuperFences/

[SFV]: https://img.shields.io/nuget/v/NuStreamDocs.SuperFences.svg?label=

[Site]: https://www.nuget.org/packages/NuStreamDocs.Sitemap/

[SiteV]: https://img.shields.io/nuget/v/NuStreamDocs.Sitemap.svg?label=

[Ver]: https://www.nuget.org/packages/NuStreamDocs.Versions/

[VerV]: https://img.shields.io/nuget/v/NuStreamDocs.Versions.svg?label=

[Tags]: https://www.nuget.org/packages/NuStreamDocs.Tags/

[TagsV]: https://img.shields.io/nuget/v/NuStreamDocs.Tags.svg?label=

[Meta]: https://www.nuget.org/packages/NuStreamDocs.Metadata/

[MetaV]: https://img.shields.io/nuget/v/NuStreamDocs.Metadata.svg?label=

[Auto]: https://www.nuget.org/packages/NuStreamDocs.Autorefs/

[AutoV]: https://img.shields.io/nuget/v/NuStreamDocs.Autorefs.svg?label=

[Xref]: https://www.nuget.org/packages/NuStreamDocs.Xrefs/

[XrefV]: https://img.shields.io/nuget/v/NuStreamDocs.Xrefs.svg?label=

[Sphinx]: https://www.nuget.org/packages/NuStreamDocs.SphinxInventory/

[SphinxV]: https://img.shields.io/nuget/v/NuStreamDocs.SphinxInventory.svg?label=

[Layouts]: https://www.nuget.org/packages/NuStreamDocs.Layouts/

[LayoutsV]: https://img.shields.io/nuget/v/NuStreamDocs.Layouts.svg?label=

[LV]: https://www.nuget.org/packages/NuStreamDocs.LinkValidator/

[LVV]: https://img.shields.io/nuget/v/NuStreamDocs.LinkValidator.svg?label=

[Serve]: https://www.nuget.org/packages/NuStreamDocs.Serve/

[ServeV]: https://img.shields.io/nuget/v/NuStreamDocs.Serve.svg?label=

[Bc]: https://www.nuget.org/packages/NuStreamDocs.Blog.Common/

[BcV]: https://img.shields.io/nuget/v/NuStreamDocs.Blog.Common.svg?label=

[Blog]: https://www.nuget.org/packages/NuStreamDocs.Blog/

[BlogV]: https://img.shields.io/nuget/v/NuStreamDocs.Blog.svg?label=

[BlogMk]: https://www.nuget.org/packages/NuStreamDocs.Blog.MkDocs/

[BlogMkV]: https://img.shields.io/nuget/v/NuStreamDocs.Blog.MkDocs.svg?label=

[Feed]: https://www.nuget.org/packages/NuStreamDocs.Feed/

[FeedV]: https://img.shields.io/nuget/v/NuStreamDocs.Feed.svg?label=

[Mer]: https://www.nuget.org/packages/NuStreamDocs.Mermaid/

[MerV]: https://img.shields.io/nuget/v/NuStreamDocs.Mermaid.svg?label=

[LB]: https://www.nuget.org/packages/NuStreamDocs.Lightbox/

[LBV]: https://img.shields.io/nuget/v/NuStreamDocs.Lightbox.svg?label=

[IcM]: https://www.nuget.org/packages/NuStreamDocs.Icons.Material/

[IcMV]: https://img.shields.io/nuget/v/NuStreamDocs.Icons.Material.svg?label=

[IcMD]: https://www.nuget.org/packages/NuStreamDocs.Icons.MaterialDesign/

[IcMDV]: https://img.shields.io/nuget/v/NuStreamDocs.Icons.MaterialDesign.svg?label=

[IcFA]: https://www.nuget.org/packages/NuStreamDocs.Icons.FontAwesome/

[IcFAV]: https://img.shields.io/nuget/v/NuStreamDocs.Icons.FontAwesome.svg?label=

[Priv]: https://www.nuget.org/packages/NuStreamDocs.Privacy/

[PrivV]: https://img.shields.io/nuget/v/NuStreamDocs.Privacy.svg?label=

[Csp]: https://www.nuget.org/packages/NuStreamDocs.Csp/

[CspV]: https://img.shields.io/nuget/v/NuStreamDocs.Csp.svg?label=

[Fonts]: https://www.nuget.org/packages/NuStreamDocs.Fonts/

[FontsV]: https://img.shields.io/nuget/v/NuStreamDocs.Fonts.svg?label=

[Trans]: https://www.nuget.org/packages/NuStreamDocs.Transitions/

[TransV]: https://img.shields.io/nuget/v/NuStreamDocs.Transitions.svg?label=

[Redir]: https://www.nuget.org/packages/NuStreamDocs.Redirects/

[RedirV]: https://img.shields.io/nuget/v/NuStreamDocs.Redirects.svg?label=

[Audit]: https://www.nuget.org/packages/NuStreamDocs.Audit/

[AuditV]: https://img.shields.io/nuget/v/NuStreamDocs.Audit.svg?label=

[CL]: https://www.nuget.org/packages/NuStreamDocs.ContentLoader/

[CLV]: https://img.shields.io/nuget/v/NuStreamDocs.ContentLoader.svg?label=

[CLFeed]: https://www.nuget.org/packages/NuStreamDocs.ContentLoader.Feed/

[CLFeedV]: https://img.shields.io/nuget/v/NuStreamDocs.ContentLoader.Feed.svg?label=

[CLGH]: https://www.nuget.org/packages/NuStreamDocs.ContentLoader.GitHub/

[CLGHV]: https://img.shields.io/nuget/v/NuStreamDocs.ContentLoader.GitHub.svg?label=

[CLOAS]: https://www.nuget.org/packages/NuStreamDocs.ContentLoader.OpenApi/

[CLOASV]: https://img.shields.io/nuget/v/NuStreamDocs.ContentLoader.OpenApi.svg?label=

[Opt]: https://www.nuget.org/packages/NuStreamDocs.Optimize/

[OptV]: https://img.shields.io/nuget/v/NuStreamDocs.Optimize.svg?label=

[Api]: https://www.nuget.org/packages/NuStreamDocs.CSharpApiGenerator/

[ApiV]: https://img.shields.io/nuget/v/NuStreamDocs.CSharpApiGenerator.svg?label=

---

## Markdown extensions

`NuStreamDocs.MarkdownExtensions` packages the common pymdownx-equivalent
extensions. `.UseCommonMarkdownExtensions()` enables the typical bundle
(admonitions, tabs, details, footnotes, definition lists, attr-list, tables,
checklists, mark, caret/tilde, critic-markup, inline-hilite, markdown-in-html).

You can also opt-in individually:

```csharp
await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseAdmonitions()
    .UseTabs()
    .UseDetails()
    .UseFootnotes()
    .UseDefinitionLists()
    .UseAttrList()
    .UseTables()
    .UseCheckLists()
    .UseMark()
    .UseCaretTilde()
    .UseCriticMarkup()
    .UseInlineHilite()
    .UseMarkdownInHtml()
    .BuildAsync();
```

---

## Hiding pages from the navigation

Three layers of "don't show this in the sidebar" — pick whichever fits the
use case. Pages hidden from the nav still build and are reachable by direct
URL; only their entry in the navigation tree is suppressed.

### Per-page (frontmatter)

Drop a flag into the page's YAML frontmatter:

```yaml
---
title: Internal notes
not_in_nav: true       # alias: nav_exclude: true
---
```

Both `not_in_nav: true` and `nav_exclude: true` are accepted. The page
still renders to disk and is search-indexed; it just doesn't appear in
the sidebar. Truthy values: `true`, `yes`. Anything else (including
`false`, `no`, omitted) keeps the page visible.

### Per-section (`.pages` file)

Drop a `.pages` file into a directory and set `hide: true` to suppress
the entire section (and every page underneath it) from the navigation:

```yaml
# guide/internal/.pages
hide: true
```

### Glob-level (build configuration)

Exclude a whole pattern at build configuration time — the pages are
still built and reachable, but never make it into the nav matcher:

```csharp
.UseNav(opts => opts with
{
    Excludes = [..opts.Excludes, "drafts/**", "**/_internal/**"],
})
```

Each layer logs a debug message naming the prune reason
(`frontmatter not_in_nav`, `.pages hide:true`, `glob excluded`) so you
can confirm what's getting hidden.

---

## Recipes

### Minimal blog

```csharp
await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme()
    .UseNav()
    .UseToc()
    .UseHighlight()
    .UseMkDocsBlog()
    .UseFeed(new FeedOptions(
        siteUrl: [.. "https://example.com"u8],
        title: [.. "Example blog"u8],
        description: [.. "Latest posts."u8],
        postsSubdirectory: (PathSegment)"blog/posts"))
    .UseSearch()
    .BuildAsync();
```

### Documentation site with API reference

```csharp
await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterial3Theme()
    .UseNav(opts => opts with { Prune = true })
    .UseToc()
    .UseHighlight()
    .UseSearch()
    .UseAutorefs()
    .UseXrefs()
    .UseCSharpApiGenerator(opts => opts with { Packages = ["MyLib"] })
    .UseCommonMarkdownExtensions()
    .UseLinkValidator(opts => opts with { Strict = true })
    .BuildAsync();
```

### Privacy-first publish

```csharp
await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme()
    .UseNav()
    .UseToc()
    .UseHighlight()
    .UseSearch()
    .UsePrivacy()                                  // localize external assets
    .UseHtmlMinify()
    .UseOptimize()                                 // pre-compressed .gz / .br
    .BuildAsync();
```

### Versioned multi-release docs

```csharp
await new DocBuilder()
    .WithInput("docs")
    .WithOutput($"site/{version}")
    .UseMaterialTheme()
    .UseNav()
    .UseSearch()
    .UseVersions(opts => opts with
    {
        Manifest = "site/versions.json",
        DefaultAlias = "latest",
        ThisVersion = version,
    })
    .BuildAsync();
```

### Academic / scientific docs with citations

```csharp
using NuStreamDocs.Bibliography;
using NuStreamDocs.Bibliography.Model;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme()
    .UseNav()
    .UseBibliography(db =>
        db.AddCase("mabo", "Mabo v Queensland (No 2)", "(1992) 175 CLR 1", 1992)
          .AddBook("gummow", "Change and Continuity",
                   PersonName.Of("William", "Gummow"),
                   2018, "Federation Press"))
    .BuildAsync();
```

Now `[@mabo]` and `[@gummow, p 23]` markers in markdown rewrite into
numbered footnote refs that resolve through the database; a `## Bibliography`
section is appended per page. AGLC4 is the default style.

For larger bibliographies, load from CSL-JSON (the canonical pandoc /
Zotero export shape):

```csharp
using NuStreamDocs.Bibliography.Csl;

var entries = CslJsonLoader.LoadFile("references.json");
var db = new BibliographyDatabaseBuilder();
foreach (var e in entries) { db.Add(e); }

builder.UseBibliography(new BibliographyOptions(
    db.Build(),
    Aglc4Style.Instance,
    WarnOnMissing: true));
```

### Material Design Icons inline (mkdocs-material parity)

`:material-rocket-launch:` and friends emit the actual MDI SVG inlined
into the page — same shape mkdocs-material produces. Pass the resolver
to `UseMaterialTheme` (or `UseMaterial3Theme`) and the theme wires it
into its icon-shortcode plugin:

```csharp
using NuStreamDocs.Icons.MaterialDesign;
using NuStreamDocs.Theme.Material;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme(new MdiIconResolver())
    .BuildAsync();
```

Same overload exists on `UseMaterial3Theme` for the Material 3 theme.
Names that aren't in the MDI catalogue fall back to a Google Material
font-ligature span automatically. The full ~7,400-icon catalogue is
baked into the assembly as `"…"u8` literals — no resource decode, no
runtime dictionary build.

### Cross-link with a Sphinx site

```csharp
using NuStreamDocs.Autorefs;
using NuStreamDocs.SphinxInventory;

var registry = new AutorefsRegistry();

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme()
    .UseAutorefs(registry)
    .UseSphinxInventory(registry, new SphinxInventoryOptions(
        ProjectName: "MyDocs",
        Version: "1.0.0",
        OutputFileName: "objects.inv"))
    .BuildAsync();
```

Emits `objects.inv` at the site root; an external Sphinx site that
adds your URL to its `intersphinx_mapping` can now `:any:`-link to
every UID your build registered.

### Reusable snippets with section markers

`docs/_partials/api-warning.md`:

```markdown
This page is regenerated from upstream metadata.

<!-- @section breaking -->

!!! warning "Breaking change"
    The 2.0 release removes the synchronous overloads.
    See the [migration guide](../migration/v2.md).

<!-- @endsection -->

<!-- @section deprecated -->

!!! info "Soft-deprecated"
    Will be removed in 3.0; use the async overload.

<!-- @endsection -->
```

In any page:

```markdown
--8<-- "_partials/api-warning.md#breaking"
```

Only the marked section is spliced in. Section markers are HTML
comments — invisible if the snippets plugin is disabled, no leftover
sigils in the rendered output.

### Repeated values via macros

```csharp
using NuStreamDocs.Macros;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMacros(opts => opts with
    {
        Variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["project"] = "ReactiveUI",
            ["version"] = "20.0.0",
            ["min_dotnet"] = ".NET 9.0",
        },
    })
    .BuildAsync();
```

`{{ project }}` / `{{ version }}` / `{{ min_dotnet }}` markers in
markdown resolve at preprocess time. Skips fenced + inline code
regions automatically.

### Live-reload dev loop

```csharp
using NuStreamDocs.Serve;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .UseMaterialTheme()
    .UseNav()
    .UseSearch()
    .UseHighlight()
    .WatchAndServeAsync(opts => opts with { OpenBrowser = true });
```

Runs the initial build, starts Kestrel on `http://127.0.0.1:8000`, and
watches `docs/` for changes. Each save triggers a debounced rebuild and
sends a reload signal over a websocket so connected browsers refresh
automatically.

### Custom plugin

```csharp
public sealed class WordCountPlugin : IDocPlugin
{
    public string Name => "wordcount";
    public ValueTask OnConfigureAsync(PluginConfigureContext ctx, CancellationToken ct) => default;
    public ValueTask OnRenderPageAsync(PluginRenderContext ctx, CancellationToken ct)
    {
        // inspect ctx.Html, append a <footer> with the word count, etc.
        return default;
    }
    public ValueTask OnFinalizeAsync(PluginFinalizeContext ctx, CancellationToken ct) => default;
}

// in Program.cs
.UsePlugin(new WordCountPlugin())
```

---

## Configuration files (mkdocs.yml / Zensical TOML)

If you have an existing mkdocs site, you can point a config reader at the YAML
instead of (or in addition to) the fluent builder:

```csharp
.UseMkDocsConfig("mkdocs.yml")        // YAML → JSON → DocBuilder, no YamlDotNet
// or
.UseZensicalConfig("zensical.toml")   // TOML → JSON → DocBuilder
```

The reader translates the config into builder calls. The fluent surface stays
the source of truth — config files are convenience translators on top.

---

## Module reference

Every assembly. The `Builder` column shows the canonical extension method on
`DocBuilder`; multiple overloads exist (no-arg, options-customizer,
options-customizer+logger).

| Assembly                              | Builder                                                      | Description                                                                                                                                                                                                                                                                                                    |
|---------------------------------------|--------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **`NuStreamDocs`**                    | (core)                                                       | AOT-friendly Markdown + static-site generator core. Custom span/UTF-8 parser, HTML emitter, page pipeline, plugin contract, content-hash incremental builds.                                                                                                                                                   |
| **`NuStreamDocs.Common`**             | (used by plugins)                                            | Shared building blocks: `DocPluginBase`, byte-level YAML scanning helpers.                                                                                                                                                                                                                                     |
| **`NuStreamDocs.Markdown.Common`**    | (used by plugins)                                            | Shared byte-level scanning helpers for Markdown rewriters — fenced-code passthrough, inline-code passthrough, line-start / end probes, indented-body detection.                                                                                                                                                |
| **`NuStreamDocs.Templating`**         | (used by themes)                                             | Span/UTF-8 Mustache-style template engine. Templates compile once to a flat instruction list and render directly to `IBufferWriter<byte>`.                                                                                                                                                                     |
| **`NuStreamDocs.Theme.Common`**       | (used by themes)                                             | Shared theme helpers — icon-shortcode rewriter, embedded-asset loader, theme-model loader, page-shell base.                                                                                                                                                                                                    |
| **`NuStreamDocs.Theme.Material`**     | `.UseMaterialTheme()`                                        | Material-styled theme. Mustache page template + Material CSS/JS bundle, all embedded.                                                                                                                                                                                                                          |
| **`NuStreamDocs.Theme.Material3`**    | `.UseMaterial3Theme()`                                       | Material Design 3 theme — design-token-driven (color roles, shape, typography, elevation).                                                                                                                                                                                                                     |
| **`NuStreamDocs.Config.MkDocs`**      | `.UseMkDocsConfig()`                                         | mkdocs.yml reader. Hand-rolled span / UTF-8 YAML→JSON pipeline; never round-trips through strings.                                                                                                                                                                                                             |
| **`NuStreamDocs.Config.Zensical`**    | `.UseZensicalConfig()`                                       | Zensical-flavored TOML config reader. Span-based and AOT-clean.                                                                                                                                                                                                                                                |
| **`NuStreamDocs.Nav`**                | `.UseNav()`                                                  | Rich navigation: glob includes, ordering hints, hidden sections, `.pages` overrides, multi-level rewrites, `navigation.prune`, orphan-page warnings.                                                                                                                                                           |
| **`NuStreamDocs.Toc`**                | `.UseToc()`                                                  | Per-page table of contents and permalink heading anchors.                                                                                                                                                                                                                                                      |
| **`NuStreamDocs.Search`**             | `.UseSearch()`                                               | Build-time search index. Pagefind-compatible sharded index by default; Lunr-compatible JSON alt.                                                                                                                                                                                                               |
| **`NuStreamDocs.Highlight`**          | `.UseHighlight()`                                            | Server-side syntax highlighter. Pygments-shape lexers via `[GeneratedRegex]`. Pygments short-form CSS classes; wraps blocks in `<div class="highlight">`. Per-block extras: `title="..."` (filename bar), opt-in copy button. Reads fence-info from the markdown emitter's `data-info` attr. No JS, no Python. |
| **`NuStreamDocs.MarkdownExtensions`** | `.UseCommonMarkdownExtensions()`                             | Common Markdown block + inline extensions — admonitions, tabs, details, checklists, mark, footnotes, definition lists, attr-list, etc.                                                                                                                                                                         |
| **`NuStreamDocs.Mermaid`**            | `.UseMermaid()`                                              | Retags fenced `mermaid` blocks; pulls the Mermaid runtime into the head.                                                                                                                                                                                                                                       |
| **`NuStreamDocs.Lightbox`**           | `.UseLightbox()`                                             | glightbox image lightbox — adds glightbox CSS/JS and wraps content images.                                                                                                                                                                                                                                     |
| **`NuStreamDocs.Privacy`**            | `.UsePrivacy()`                                              | Localizes external assets (img/link/script) under `assets/external/`; rewrites HTML to local paths. Byte-level UTF-8 throughout.                                                                                                                                                                               |
| **`NuStreamDocs.Optimize`**           | `.UseOptimize()`, `.UseHtmlMinify()`                         | Output optimizer. HTML minify pass + pre-compressed `.gz` / `.br` siblings (truly-async .NET 10 stream APIs).                                                                                                                                                                                                  |
| **`NuStreamDocs.LinkValidator`**      | `.UseLinkValidator()`                                        | Strict link validator. Internal mode (relative + anchors + nav/disk consistency); optional external HTTP-HEAD mode via Polly with host-batched throttling and retry.                                                                                                                                           |
| **`NuStreamDocs.Serve`**              | `.WatchAndServeAsync()`                                      | Watch + dev-server. Initial build, then a long-running loop: FileSystemWatcher + debounce → rebuild → signal connected browsers via LiveReload websocket. Kestrel-hosted; not AOT-compatible (opt-in package, separate from the AOT-clean core).                                                               |
| **`NuStreamDocs.Versions`**           | `.UseVersions()`                                             | mike-equivalent versioning. Publishes a `versions.json` manifest themes can render a selector against.                                                                                                                                                                                                         |
| **`NuStreamDocs.Sitemap`**            | `.UseSitemap()`, `.UseNotFoundPage()`, `.UseRedirects()`     | Site-level emitters — sitemap.xml + robots.txt, default 404 page, redirect stubs.                                                                                                                                                                                                                              |
| **`NuStreamDocs.Tags`**               | `.UseTags()`                                                 | Collects per-page `tags:` frontmatter; emits a tags index plus per-tag listing pages.                                                                                                                                                                                                                          |
| **`NuStreamDocs.Metadata`**           | `.UseMetadata()`                                             | Directory-level (`_meta.yml`) + sidecar (`page.meta.yml`) frontmatter merging spliced into pages before render. Inspired by Statiq's directory/sidecar/computed metadata model.                                                                                                                                |
| **`NuStreamDocs.Autorefs`**           | `.UseAutorefs()`                                             | Cross-document reference resolver. Collects heading anchor IDs during render; rewrites `@autoref:ID` markers to the resolved page URL + fragment.                                                                                                                                                              |
| **`NuStreamDocs.Xrefs`**              | `.UseXrefs()`                                                | DocFX-style xrefmap. Emits `xrefmap.json` at finalize; optionally consumes external xrefmaps at configure. Resolves cross-site UIDs via Autorefs.                                                                                                                                                              |
| **`NuStreamDocs.SuperFences`**        | `.UseSuperFences()`                                          | Custom-fence dispatcher. Auto-discovers `ICustomFenceHandler` plugins and rewrites `<pre><code class="language-X">` blocks claimed by a registered handler.                                                                                                                                                    |
| **`NuStreamDocs.Snippets`**           | `.UseSnippets()`                                             | pymdownx.snippets — `--8<-- "file"` includes spliced inline at preprocess time.                                                                                                                                                                                                                                |
| **`NuStreamDocs.Macros`**             | `.UseMacros()`                                               | mkdocs-macros-equivalent variable substitution. `{{ name }}` markers resolve through a host-supplied dictionary; fenced and inline code regions pass through untouched. Optional HTML escaping; optional `Warning`-level logging for unresolved names.                                                         |
| **`NuStreamDocs.Arithmatex`**         | `.UseArithmatex()`                                           | pymdownx.arithmatex (generic mode) — wraps `$x$` / `$$x$$` for client-side renderers.                                                                                                                                                                                                                          |
| **`NuStreamDocs.Emoji`**              | `.UseEmoji()`                                                | pymdownx.emoji default — `:name:` shortcodes become twemoji spans backed by a built-in popular-emoji index.                                                                                                                                                                                                    |
| **`NuStreamDocs.Keys`**               | `.UseKeys()`                                                 | pymdownx.keys — `++ctrl+alt+del++` becomes a structured keys span.                                                                                                                                                                                                                                             |
| **`NuStreamDocs.MagicLink`**          | `.UseMagicLink()`                                            | pymdownx.magiclink default — bare http(s)/ftp(s)/mailto/www URLs become autolinks.                                                                                                                                                                                                                             |
| **`NuStreamDocs.SmartSymbols`**       | `.UseSmartSymbols()`                                         | pymdownx.smartsymbols — © ® ™, c/o, ±, ≠, arrow forms, common fractions.                                                                                                                                                                                                                                       |
| **`NuStreamDocs.Icons.Material`**     | `.UseMaterialIcons()`                                        | Google Material Icons + Material Symbols (Outlined / Rounded / Sharp). Pairs with any theme.                                                                                                                                                                                                                   |
| **`NuStreamDocs.Icons.FontAwesome`**  | `.UseFontAwesome()`                                          | Font Awesome Free from a configurable CDN. Pairs with any theme.                                                                                                                                                                                                                                               |
| **`NuStreamDocs.Blog.Common`**        | (used by blog plugins)                                       | Shared blog pipeline. Frontmatter reader, post scanner, markdown emitters, generation orchestration.                                                                                                                                                                                                           |
| **`NuStreamDocs.Blog`**               | `.UseWyamBlog()`                                             | Wyam-compatible blog — flat directory of `YYYY-MM-DD-slug.md` posts, Wyam-style frontmatter (NoTitle / IsBlog / Title / Tags / Author / Published), index + tag archives.                                                                                                                                      |
| **`NuStreamDocs.Blog.MkDocs`**        | `.UseMkDocsBlog()`                                           | mkdocs-material-style blog — posts under `blog/posts/` with categories/date/authors frontmatter; index + category archives.                                                                                                                                                                                    |
| **`NuStreamDocs.Feed`**               | `.UseFeed()`                                                 | RSS 2.0 / Atom feeds. Reuses the blog scanner so the same source powers the blog and the feed.                                                                                                                                                                                                                 |
| **`NuStreamDocs.CSharpApiGenerator`** | `.UseCSharpApiGenerator()`, `.UseCSharpApiGeneratorDirect()` | C# reference generator. Wraps SourceDocParser + Zensical emitter. Four input shapes: NuGet packages, DLLs, manifest, custom callback.                                                                                                                                                                          |

---

## Why pick NuStreamDocs

For a small site, every modern static-site generator is fast enough. The
difference shows up when an editor reruns the build on every save, when
CI pays for every minute, or when the corpus grows past a few hundred
pages. NuStreamDocs is built for those shapes. Numbers are from
BenchmarkDotNet on a single workstation (AMD Ryzen 7 5800X, 16 logical
cores, .NET 10.0.7, Release config — see `src/benchmarks/`).

### End-to-end build, real-world corpus (211 pages, 7.6 MB)

The reference fixture is a snapshot of the ReactiveUI website docs.
`Baseline` is parse + render + emit; everything else layers a plugin on
top of the same input.

| Scenario                         |        Time | Allocated | Per-page |
|----------------------------------|------------:|----------:|---------:|
| Baseline (parse → render → emit) | **11.2 ms** |    770 KB |    53 µs |
| + markdown extensions            |     13.9 ms |   1.96 MB |    66 µs |
| + syntax highlighter             |     23.3 ms |   2.14 MB |   110 µs |
| + nav (full discovery)           |     20.6 ms |   2.24 MB |    98 µs |
| + bibliography                   |     11.3 ms |    901 KB |    54 µs |
| + magic-link / autolinks         |     13.7 ms |    956 KB |    65 µs |
| + every plugin (Full stack)      | **87.9 ms** |   13.2 MB |   417 µs |

That's the **whole 211-page corpus, every plugin enabled, in 88 ms** —
**~2,400 pages / second** with the works on. Without plugins the parse-
render-emit core handles **~19,000 pages / second** on the same box.

### Per-page synthetic (no I/O, pure pipeline)

| Pages |            Baseline |           Full stack |
|-------|--------------------:|---------------------:|
| 50    | 1.1 ms (22 µs/page) |  3.9 ms (79 µs/page) |
| 500   | 7.6 ms (15 µs/page) | 28.8 ms (58 µs/page) |

The per-page cost goes *down* at 500 pages because JIT tiering kicks in
and the parallel scheduler amortizes its overhead.

Per-plugin micro-benchmarks, syntax-highlighter throughput by language,
zero-allocation render-core measurements, and the full cross-suite
allocation + CPU profile are in [BENCHMARKS.md](BENCHMARKS.md).

### Native AOT-ready

The library assemblies build with `IsAotCompatible=true` and the trim
analyzer enabled; reflection-using dependencies (BenchmarkDotNet,
Verify) are quarantined to test + benchmark projects. A published build
is a single native executable that starts in milliseconds — useful for
CI containers and pre-commit hooks where steady-state cost matters less
than startup.

### Reproducibility

Every plugin in the pipeline is byte-deterministic given the same input.
Same content + same plugins + same options = same output bytes — useful
for diffing, caching, and `git`-friendly site directories. The
`src/benchmarks/` harness re-runs on every perf-affecting change;
per-plugin benchmarks plus the end-to-end rxui-corpus profile keep
regressions visible.

---

## Testing

- **TUnit + Verify** under `src/tests`, run via Microsoft Testing Platform.
  1,745 tests across the assemblies.
- **BenchmarkDotNet** under `src/benchmarks` — full-pipeline + per-stage +
  per-pattern benchmarks with `MemoryDiagnoser` enabled.

```bash
cd src
dotnet test --solution NuStreamDocs.slnx
```

---

## Contributing

The project's coding-style and engineering rules live in
[CONTRIBUTING.md](CONTRIBUTING.md). [`CLAUDE.md`](CLAUDE.md) is the
machine-facing companion with hot-path performance rules — read both before
opening a PR. Key points:

- Allman braces, file-scoped namespaces, expression-bodied members,
  `_camelCase` privates, `var` for locals.
- `[LoggerMessage]` source-generated logging only — no `logger.LogX("…")`
  calls.
- Production code follows the strict allocation-discipline rules in
  `CLAUDE.md` (zero LINQ, prefer `for` over `foreach`, byte-level scanners,
  `Span<T>` + range expressions, etc.). Test code is exempt.

---

## Acknowledgements

NuStreamDocs stands on the shoulders of several outstanding documentation
generators. Where a feature here mirrors a pattern from one of these
projects, that project taught us how to do it well — and we're grateful
they put their work under licenses that let us learn from them.

- **[mkdocs-material](https://squidfunk.github.io/mkdocs-material/)
  ** ([repo](https://github.com/squidfunk/mkdocs-material)) — MIT.
  The Material theme assets we embed (page templates, partials, default
  CSS classes, icon-shortcode shapes) come from mkdocs-material. Martin
  Donath's work is the visual + UX baseline our theme aims to match.
  Thanks to Squidfunk and the mkdocs-material contributors.

- **[mkdocs](https://www.mkdocs.org/)** ([repo](https://github.com/mkdocs/mkdocs)) — BSD-2-Clause.
  The plugin model, nav configuration shape, and `mkdocs.yml` schema
  `NuStreamDocs.Config.MkDocs` reads all come from upstream mkdocs.
  Thanks to Tom Christie and the mkdocs maintainers.

- **[Zensical](https://zensical.org/)** ([repo](https://github.com/zensical/zensical)) — MIT.
  The Rust + Python successor to mkdocs-material. We use Zensical as the
  behavioral reference for nav / search / blog / privacy plugins, and
  for the Zensical TOML config shape. Thanks to the Zensical maintainers.

- **[Statiq.Framework](https://www.statiq.dev/)** ([repo](https://github.com/statiqdev/Statiq.Framework)) — MIT.
  The fluent builder model, plugin-pipeline shape, and directory / sidecar
  / computed metadata patterns are inspired by Statiq. Thanks to Dave
  Glick. *(Note: only Statiq.Framework is referenced — Statiq.Docs is
  under a non-commercial license and is not used here.)*

- **[DocFX](https://dotnet.github.io/docfx/)** ([repo](https://github.com/dotnet/docfx)) — MIT.
  The metadata-extraction pipeline in `NuStreamDocs.CSharpApiGenerator`
  lifts patterns from DocFX — assembly walking, source-link resolution,
  and the `xrefmap.json` shape (`NuStreamDocs.Xrefs`). Thanks to the .NET
  Foundation and the DocFX team.

- **[Material Design Icons (Pictogrammers)](https://pictogrammers.com/library/mdi/)
  ** ([repo](https://github.com/Templarian/MaterialDesign-SVG)) — Pictogrammers Free License (icons under Apache 2.0).
  `NuStreamDocs.Icons.MaterialDesign` embeds the entire MDI catalogue
  (~7,400 icons) as inline-SVG path data baked into the assembly at
  build time so `:material-foo:` shortcodes match the markup
  mkdocs-material emits. Thanks to the Pictogrammers team for keeping
  this catalogue free, open source, and friendly to redistribute.

- **[Material Web](https://github.com/material-components/material-web)** — Apache 2.0.
  `NuStreamDocs.Theme.Material3` vendors a small official Material Web
  browser-runtime subset for the search field and icon-button controls,
  then layers docs-specific layout and parity CSS on top. Thanks to the
  Material Web team.

- **[Lit](https://github.com/lit/lit)** and **[tslib](https://github.com/Microsoft/tslib)** — BSD-3-Clause / 0BSD.
  These are redistributed only as transitive browser-runtime
  dependencies of the vendored Material Web subset used by
  `NuStreamDocs.Theme.Material3`. Thanks to the Lit team and Microsoft.

All nine upstream projects ship under permissive licenses compatible
with this project's MIT license. Their license texts are reproduced
verbatim in [LICENSE](LICENSE) under the **Third-Party Notices**
section.

---

## License

MIT — see [LICENSE](LICENSE).

The repository also redistributes a small number of third-party files
inside `NuStreamDocs.Theme.Material3` under their original licenses.
See [LICENSE](LICENSE) and
`src/NuStreamDocs.Theme.Material3/Templates/THIRD_PARTY_NOTICES.md`.
