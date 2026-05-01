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
  (`OnConfigure` / `OnRenderPage` / `OnFinalise`), so most ordering is
  automatic.
- **Plugins are pluggable units**, not magical reflection scans. If you want
  a custom plugin, write one `IDocPlugin` and `.UsePlugin(myPlugin)`.

---

## Themes

Pick exactly one theme assembly:

| Package | What |
|---|---|
| `NuStreamDocs.Theme.Material` | Classic mkdocs-material look. Mustache page template + Material CSS/JS bundle, all embedded. |
| `NuStreamDocs.Theme.Material3` | Material Design 3 — design-token-driven (color roles, shape, typography, elevation), structurally inspired by mkdocs-material 9.x but rebuilt for the modern MD3 surface. |

```csharp
.UseMaterialTheme()                                            // defaults
.UseMaterialTheme(opts => opts with { AssetSource = ... })     // CDN vs embedded
.UseMaterial3Theme()
```

Both themes honour the same `IStaticAssetProvider` /
`IHeadExtraProvider` contracts, so any plugin written once works under either
theme without recompilation.

---

## Plugins (overview)

Every plugin ships in its own NuGet so you only pay for what you use.

### Essentials (most sites want all of these)

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.Nav` | `.UseNav()` | Glob includes, ordering hints, hidden sections, `.pages` overrides, `navigation.prune`, orphan-page warnings. |
| `NuStreamDocs.Toc` | `.UseToc()` | Per-page table of contents and permalink heading anchors. |
| `NuStreamDocs.Highlight` | `.UseHighlight()` | Server-side syntax highlighting. TextMate JSON grammars + `[GeneratedRegex]`. No JS runtime. Pygments CSS-class output for theme parity. |
| `NuStreamDocs.Search` | `.UseSearch()` | Build-time search index. Pagefind-compatible by default; Lunr-compatible alt. |
| `NuStreamDocs.MarkdownExtensions` | `.UseCommonMarkdownExtensions()` | Admonitions, content tabs, collapsible details, check-lists, mark spans, footnotes, definition lists. |

### Markdown syntax extensions (à la carte)

Each is a separate assembly so you only pull what you use:

| Package | Builder | Syntax |
|---|---|---|
| `NuStreamDocs.Abbr` | `.UseAbbreviations()` | `*[token]: definition` → `<abbr>` |
| `NuStreamDocs.Arithmatex` | `.UseArithmatex()` | `$x$` / `$$x$$` math (MathJax/KaTeX-friendly markup) |
| `NuStreamDocs.Emoji` | `.UseEmoji()` | `:name:` → twemoji `<span>` |
| `NuStreamDocs.Keys` | `.UseKeys()` | `++ctrl+alt+del++` → keyboard `<span>` |
| `NuStreamDocs.MagicLink` | `.UseMagicLink()` | Bare URLs become autolinks |
| `NuStreamDocs.SmartSymbols` | `.UseSmartSymbols()` | © ® ™, c/o, ±, ≠, → … |
| `NuStreamDocs.Snippets` | `.UseSnippets()` | `--8<-- "file"` includes |
| `NuStreamDocs.SuperFences` | `.UseSuperFences()` | Custom-fence dispatcher (auto-discovers fence handlers) |

### Site emitters

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.Sitemap` | `.UseSitemap()`, `.UseNotFoundPage()`, `.UseRedirects()` | sitemap.xml + robots.txt, default 404, redirect stubs |
| `NuStreamDocs.Versions` | `.UseVersions()` | mike-equivalent versioning manifest (`versions.json`) |
| `NuStreamDocs.Tags` | `.UseTags()` | Tag index + per-tag listing pages from `tags:` frontmatter |
| `NuStreamDocs.Metadata` | `.UseMetadata()` | Directory-level (`_meta.yml`) + sidecar (`page.meta.yml`) frontmatter merging (Statiq-inspired) |
| `NuStreamDocs.Autorefs` | `.UseAutorefs()` | `@autoref:ID` rewriting against collected heading anchors |
| `NuStreamDocs.Xrefs` | `.UseXrefs()` | DocFX-style `xrefmap.json` emit + import |

### Quality / validation

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.LinkValidator` | `.UseLinkValidator()` | Strict-mode link checker. Internal: relative + anchors + nav/disk consistency (mkdocs `--strict`). External: HTTP HEAD via Polly with host-batched throttling/retry. |

### Blog & syndication

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.Blog` | `.UseWyamBlog()` | Wyam-flavoured blog: `YYYY-MM-DD-slug.md` + Wyam frontmatter (NoTitle/IsBlog/Title/Tags/Author/Published). |
| `NuStreamDocs.Blog.MkDocs` | `.UseMkDocsBlog()` | mkdocs-material flavoured: posts under `blog/posts/` with categories/date/authors frontmatter. |
| `NuStreamDocs.Feed` | `.UseFeed()` | RSS 2.0 / Atom feed generation off the same blog scanner. |

### Media

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.Mermaid` | `.UseMermaid()` | Retag fenced `mermaid` blocks for the Mermaid runtime. |
| `NuStreamDocs.Lightbox` | `.UseLightbox()` | glightbox: wraps content images in lightbox triggers, ships glightbox CSS/JS. |
| `NuStreamDocs.Icons.Material` | `.UseMaterialIcons()` | Google Material Icons / Material Symbols (Outlined/Rounded/Sharp). |
| `NuStreamDocs.Icons.FontAwesome` | `.UseFontAwesome()` | Font Awesome Free from a configurable CDN. |

### Privacy & optimisation

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.Privacy` | `.UsePrivacy()` | Localises external assets under `assets/external/` and rewrites HTML to point at the local copies. Byte-level UTF-8 throughout. |
| `NuStreamDocs.Optimise` | `.UseOptimise()`, `.UseHtmlMinify()` | Pre-compresses emitted output as `.gz` / `.br` siblings (truly-async .NET 10 stream APIs). HTML minify pass. |

### C# API reference

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.CSharpApiGenerator` | `.UseCSharpApiGenerator()`, `.UseCSharpApiGeneratorDirect()` | Wraps SourceDocParser + the Zensical emitter. Pulls NuGet-packaged assemblies (or DLLs / manifest / custom callbacks), generates Markdown reference pages into the docs tree, and lets normal page discovery pick them up. |

---

## Markdown extensions

`NuStreamDocs.MarkdownExtensions` packages the common pymdownx-equivalent
extensions. `.UseCommonMarkdownExtensions()` enables the typical bundle
(admonitions, tabs, details, footnotes, definition lists, attr-list, tables,
checklists, mark, caret/tilde, critic-markup, inline-hilite, markdown-in-html).

You can also opt-in individually:

```csharp
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
```

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
    .UseFeed(opts => opts with { SiteUrl = "https://example.com" })
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
    .UsePrivacy()                                  // localise external assets
    .UseHtmlMinify()
    .UseOptimise()                                 // pre-compressed .gz / .br
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
    public ValueTask OnFinaliseAsync(PluginFinaliseContext ctx, CancellationToken ct) => default;
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
`DocBuilder`; multiple overloads exist (no-arg, options-customiser,
options-customiser+logger).

| Assembly | Builder | Description |
|---|---|---|
| **`NuStreamDocs`** | (core) | AOT-friendly Markdown + static-site generator core. Custom span/UTF-8 parser, HTML emitter, page pipeline, plugin contract, content-hash incremental builds. |
| **`NuStreamDocs.Common`** | (used by plugins) | Shared building blocks: `DocPluginBase`, byte-level YAML scanning helpers. |
| **`NuStreamDocs.Markdown.Common`** | (used by plugins) | Shared byte-level scanning helpers for Markdown rewriters — fenced-code passthrough, inline-code passthrough, line-start / end probes, indented-body detection. |
| **`NuStreamDocs.Templating`** | (used by themes) | Span/UTF-8 Mustache-style template engine. Templates compile once to a flat instruction list and render directly to `IBufferWriter<byte>`. |
| **`NuStreamDocs.Theme.Common`** | (used by themes) | Shared theme helpers — icon-shortcode rewriter, embedded-asset loader, theme-model loader, page-shell base. |
| **`NuStreamDocs.Theme.Material`** | `.UseMaterialTheme()` | Material-styled theme. Mustache page template + Material CSS/JS bundle, all embedded. |
| **`NuStreamDocs.Theme.Material3`** | `.UseMaterial3Theme()` | Material Design 3 theme — design-token-driven (color roles, shape, typography, elevation). |
| **`NuStreamDocs.Config.MkDocs`** | `.UseMkDocsConfig()` | mkdocs.yml reader. Hand-rolled span / UTF-8 YAML→JSON pipeline; never round-trips through strings. |
| **`NuStreamDocs.Config.Zensical`** | `.UseZensicalConfig()` | Zensical-flavoured TOML config reader. Span-based and AOT-clean. |
| **`NuStreamDocs.Nav`** | `.UseNav()` | Rich navigation: glob includes, ordering hints, hidden sections, `.pages` overrides, multi-level rewrites, `navigation.prune`, orphan-page warnings. |
| **`NuStreamDocs.Toc`** | `.UseToc()` | Per-page table of contents and permalink heading anchors. |
| **`NuStreamDocs.Search`** | `.UseSearch()` | Build-time search index. Pagefind-compatible sharded index by default; Lunr-compatible JSON alt. |
| **`NuStreamDocs.Highlight`** | `.UseHighlight()` | Server-side syntax highlighter. Pygments-shape lexers via `[GeneratedRegex]`. Pygments short-form CSS classes. No JS, no Python. |
| **`NuStreamDocs.MarkdownExtensions`** | `.UseCommonMarkdownExtensions()` | Common Markdown block + inline extensions — admonitions, tabs, details, checklists, mark, footnotes, definition lists, attr-list, etc. |
| **`NuStreamDocs.Mermaid`** | `.UseMermaid()` | Retags fenced `mermaid` blocks; pulls the Mermaid runtime into the head. |
| **`NuStreamDocs.Lightbox`** | `.UseLightbox()` | glightbox image lightbox — adds glightbox CSS/JS and wraps content images. |
| **`NuStreamDocs.Privacy`** | `.UsePrivacy()` | Localises external assets (img/link/script) under `assets/external/`; rewrites HTML to local paths. Byte-level UTF-8 throughout. |
| **`NuStreamDocs.Optimise`** | `.UseOptimise()`, `.UseHtmlMinify()` | Output optimiser. HTML minify pass + pre-compressed `.gz` / `.br` siblings (truly-async .NET 10 stream APIs). |
| **`NuStreamDocs.LinkValidator`** | `.UseLinkValidator()` | Strict link validator. Internal mode (relative + anchors + nav/disk consistency); optional external HTTP-HEAD mode via Polly with host-batched throttling and retry. |
| **`NuStreamDocs.Versions`** | `.UseVersions()` | mike-equivalent versioning. Publishes a `versions.json` manifest themes can render a selector against. |
| **`NuStreamDocs.Sitemap`** | `.UseSitemap()`, `.UseNotFoundPage()`, `.UseRedirects()` | Site-level emitters — sitemap.xml + robots.txt, default 404 page, redirect stubs. |
| **`NuStreamDocs.Tags`** | `.UseTags()` | Collects per-page `tags:` frontmatter; emits a tags index plus per-tag listing pages. |
| **`NuStreamDocs.Metadata`** | `.UseMetadata()` | Directory-level (`_meta.yml`) + sidecar (`page.meta.yml`) frontmatter merging spliced into pages before render. Inspired by Statiq's directory/sidecar/computed metadata model. |
| **`NuStreamDocs.Autorefs`** | `.UseAutorefs()` | Cross-document reference resolver. Collects heading anchor IDs during render; rewrites `@autoref:ID` markers to the resolved page URL + fragment. |
| **`NuStreamDocs.Xrefs`** | `.UseXrefs()` | DocFX-style xrefmap. Emits `xrefmap.json` at finalise; optionally consumes external xrefmaps at configure. Resolves cross-site UIDs via Autorefs. |
| **`NuStreamDocs.SuperFences`** | `.UseSuperFences()` | Custom-fence dispatcher. Auto-discovers `ICustomFenceHandler` plugins and rewrites `<pre><code class="language-X">` blocks claimed by a registered handler. |
| **`NuStreamDocs.Snippets`** | `.UseSnippets()` | pymdownx.snippets — `--8<-- "file"` includes spliced inline at preprocess time. |
| **`NuStreamDocs.Abbr`** | `.UseAbbreviations()` | Markdown Extra abbreviations — strips `*[token]: definition` lines and wraps occurrences in `<abbr>`. |
| **`NuStreamDocs.Arithmatex`** | `.UseArithmatex()` | pymdownx.arithmatex (generic mode) — wraps `$x$` / `$$x$$` for client-side renderers. |
| **`NuStreamDocs.Emoji`** | `.UseEmoji()` | pymdownx.emoji default — `:name:` shortcodes become twemoji spans backed by a built-in popular-emoji index. |
| **`NuStreamDocs.Keys`** | `.UseKeys()` | pymdownx.keys — `++ctrl+alt+del++` becomes a structured keys span. |
| **`NuStreamDocs.MagicLink`** | `.UseMagicLink()` | pymdownx.magiclink default — bare http(s)/ftp(s)/mailto/www URLs become autolinks. |
| **`NuStreamDocs.SmartSymbols`** | `.UseSmartSymbols()` | pymdownx.smartsymbols — © ® ™, c/o, ±, ≠, arrow forms, common fractions. |
| **`NuStreamDocs.Icons.Material`** | `.UseMaterialIcons()` | Google Material Icons + Material Symbols (Outlined / Rounded / Sharp). Pairs with any theme. |
| **`NuStreamDocs.Icons.FontAwesome`** | `.UseFontAwesome()` | Font Awesome Free from a configurable CDN. Pairs with any theme. |
| **`NuStreamDocs.Blog.Common`** | (used by blog plugins) | Shared blog pipeline. Frontmatter reader, post scanner, markdown emitters, generation orchestration. |
| **`NuStreamDocs.Blog`** | `.UseWyamBlog()` | Wyam-compatible blog — flat directory of `YYYY-MM-DD-slug.md` posts, Wyam-style frontmatter (NoTitle / IsBlog / Title / Tags / Author / Published), index + tag archives. |
| **`NuStreamDocs.Blog.MkDocs`** | `.UseMkDocsBlog()` | mkdocs-material-style blog — posts under `blog/posts/` with categories/date/authors frontmatter; index + category archives. |
| **`NuStreamDocs.Feed`** | `.UseFeed()` | RSS 2.0 / Atom feeds. Reuses the blog scanner so the same source powers the blog and the feed. |
| **`NuStreamDocs.CSharpApiGenerator`** | `.UseCSharpApiGenerator()`, `.UseCSharpApiGeneratorDirect()` | C# reference generator. Wraps SourceDocParser + Zensical emitter. Four input shapes: NuGet packages, DLLs, manifest, custom callback. |

---

## Performance and AOT

The whole pipeline is built around a few non-negotiables:

- **UTF-8 in, UTF-8 out** — no `string` materialisation in parse or emit.
  Scanners take `ReadOnlySpan<byte>`; emitters write to
  `IBufferWriter<byte>`.
- **Block descriptors are 16-byte structs** indexing the source buffer.
  Parsing a 10 MB doc is `O(blocks)` allocations, not `O(bytes)`.
- **Source-generated regex only.** No `System.Text.RegularExpressions` at
  runtime without a generator.
- **`SearchValues<byte>`** for delimiter scans; `IndexOfAny` runs vectorised.
- **`IsAotCompatible=true`** enforced. Trim analyser is on. Reflection-using
  deps quarantined to test/benchmark projects.
- **Bounded, time-aware caches** — parsed AST and search shards live in a
  size-capped LRU keyed by content hash; entries evict by both count and age
  so a long-running watcher doesn't accrete memory.
- **Per-page work is pure.** A page render reads only its own bytes plus
  immutable shared indexes (nav, autorefs, search vocabulary). Parse/emit
  is trivially parallel via `Parallel.ForEachAsync`.
- **Content-hash incremental builds.** Each input file's xxHash3 is
  manifest-tracked; an unchanged hash short-circuits parse + emit. Targets
  sub-second rebuilds on a one-file edit.
- **Pooled buffers everywhere.** Scanner blocks, escaper output, HTML emitter
  buffers — all rented from `ArrayPool<T>` or a `PageBuilderPool`.
- **`ConfigureAwait(false)` on every library `await`.** No exceptions.

The reference workload is **13.8K markdown files / 72 MB** (the rxui website
corpus). The `BenchmarkDotNet` harness under `src/benchmarks/` tracks
allocations and time across the pipeline, with EventPipe-traced phases for
GC-verbose hotspots.

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

## License

MIT.
