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
| `NuStreamDocs.Highlight` | `.UseHighlight()` | Server-side syntax highlighting. TextMate JSON grammars + `[GeneratedRegex]`. Wraps blocks in `<div class="highlight">` (Pygments / mkdocs-material convention); reads per-block fence-info attrs (`title="..."` for filename bar, opt-in copy button). No JS runtime. |
| `NuStreamDocs.Search` | `.UseSearch()` | Build-time search index. Pagefind-compatible by default; Lunr-compatible alt. |
| `NuStreamDocs.MarkdownExtensions` | `.UseCommonMarkdownExtensions()` | Admonitions, content tabs, collapsible details, check-lists, mark spans, footnotes, definition lists. |

### Authoring helpers

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.Macros` | `.UseMacros()` | Variable substitution. `{{ name }}` markers in markdown resolve through a host-supplied `Dictionary<string, string>`. Skips fenced + inline code regions. Closes the mkdocs-macros gap (variable substitution slice). |
| `NuStreamDocs.Bibliography` | `.UseBibliography()` | Pandoc-style citations — `[@key]` / `[@key, p 23]` / `[@a; @b]` markers resolve through a `BibliographyDatabase` (fluent builder, CSL-JSON loader). Emits a numbered footnote per citation plus a bibliography section. Ships **AGLC4** (Australian Guide to Legal Citation, 4th ed) as the v1 style; CSL-aware data model so other styles slot in. Byte-level UTF-8, zero-string-alloc on the format hot path. |

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
| `NuStreamDocs.Snippets` | `.UseSnippets()` | Whole-file (`--8<-- "file"`) and section (`--8<-- "file#name"`) includes. Section boundaries inside snippet files use HTML comments — `<!-- @section name -->` / `<!-- @endsection -->` — invisible in any CommonMark renderer even when the plugin isn't loaded. |
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
| `NuStreamDocs.SphinxInventory` | `.UseSphinxInventory()` | Sphinx-compatible `objects.inv` emitter. Snapshots the shared autorefs registry at finalise time and writes a v2 inventory file (zlib-compressed body) so external Sphinx sites can intersphinx-link into NuStreamDocs builds. |

### Quality / validation

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.LinkValidator` | `.UseLinkValidator()` | Strict-mode link checker. Internal: relative + anchors + nav/disk consistency (mkdocs `--strict`). External: HTTP HEAD via Polly with host-batched throttling/retry. |

### Dev experience

| Package | Builder | What |
|---|---|---|
| `NuStreamDocs.Serve` | `.WatchAndServeAsync()` | Long-running watch + dev-server loop. FileSystemWatcher with debounced rebuild + Kestrel-hosted static file server + LiveReload websocket so connected browsers refresh on every successful rebuild. |

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
| `NuStreamDocs.Icons.Material` | `.UseMaterialIcons()` | Google Material Icons / Material Symbols (Outlined/Rounded/Sharp) — emits the stylesheet `<link>` so font-ligature spans render. |
| `NuStreamDocs.Icons.MaterialDesign` | `new MdiIconResolver()` | **Inline-SVG** Material Design Icons (Pictogrammers MDI, ~7,400 icons). Plugs into the icon-shortcode rewriter as an `IIconResolver` so `:material-foo:` shortcodes inline the actual SVG path data — matches what mkdocs-material emits and works for the much larger MDI namespace (which Google Material Symbols doesn't fully cover). Path data is baked into a generated bucket-by-length switch; zero startup cost, ~50 ns lookup. |
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
| **`NuStreamDocs.Highlight`** | `.UseHighlight()` | Server-side syntax highlighter. Pygments-shape lexers via `[GeneratedRegex]`. Pygments short-form CSS classes; wraps blocks in `<div class="highlight">`. Per-block extras: `title="..."` (filename bar), opt-in copy button. Reads fence-info from the markdown emitter's `data-info` attr. No JS, no Python. |
| **`NuStreamDocs.MarkdownExtensions`** | `.UseCommonMarkdownExtensions()` | Common Markdown block + inline extensions — admonitions, tabs, details, checklists, mark, footnotes, definition lists, attr-list, etc. |
| **`NuStreamDocs.Mermaid`** | `.UseMermaid()` | Retags fenced `mermaid` blocks; pulls the Mermaid runtime into the head. |
| **`NuStreamDocs.Lightbox`** | `.UseLightbox()` | glightbox image lightbox — adds glightbox CSS/JS and wraps content images. |
| **`NuStreamDocs.Privacy`** | `.UsePrivacy()` | Localises external assets (img/link/script) under `assets/external/`; rewrites HTML to local paths. Byte-level UTF-8 throughout. |
| **`NuStreamDocs.Optimise`** | `.UseOptimise()`, `.UseHtmlMinify()` | Output optimiser. HTML minify pass + pre-compressed `.gz` / `.br` siblings (truly-async .NET 10 stream APIs). |
| **`NuStreamDocs.LinkValidator`** | `.UseLinkValidator()` | Strict link validator. Internal mode (relative + anchors + nav/disk consistency); optional external HTTP-HEAD mode via Polly with host-batched throttling and retry. |
| **`NuStreamDocs.Serve`** | `.WatchAndServeAsync()` | Watch + dev-server. Initial build, then a long-running loop: FileSystemWatcher + debounce → rebuild → signal connected browsers via LiveReload websocket. Kestrel-hosted; not AOT-compatible (opt-in package, separate from the AOT-clean core). |
| **`NuStreamDocs.Versions`** | `.UseVersions()` | mike-equivalent versioning. Publishes a `versions.json` manifest themes can render a selector against. |
| **`NuStreamDocs.Sitemap`** | `.UseSitemap()`, `.UseNotFoundPage()`, `.UseRedirects()` | Site-level emitters — sitemap.xml + robots.txt, default 404 page, redirect stubs. |
| **`NuStreamDocs.Tags`** | `.UseTags()` | Collects per-page `tags:` frontmatter; emits a tags index plus per-tag listing pages. |
| **`NuStreamDocs.Metadata`** | `.UseMetadata()` | Directory-level (`_meta.yml`) + sidecar (`page.meta.yml`) frontmatter merging spliced into pages before render. Inspired by Statiq's directory/sidecar/computed metadata model. |
| **`NuStreamDocs.Autorefs`** | `.UseAutorefs()` | Cross-document reference resolver. Collects heading anchor IDs during render; rewrites `@autoref:ID` markers to the resolved page URL + fragment. |
| **`NuStreamDocs.Xrefs`** | `.UseXrefs()` | DocFX-style xrefmap. Emits `xrefmap.json` at finalise; optionally consumes external xrefmaps at configure. Resolves cross-site UIDs via Autorefs. |
| **`NuStreamDocs.SuperFences`** | `.UseSuperFences()` | Custom-fence dispatcher. Auto-discovers `ICustomFenceHandler` plugins and rewrites `<pre><code class="language-X">` blocks claimed by a registered handler. |
| **`NuStreamDocs.Snippets`** | `.UseSnippets()` | pymdownx.snippets — `--8<-- "file"` includes spliced inline at preprocess time. |
| **`NuStreamDocs.Macros`** | `.UseMacros()` | mkdocs-macros-equivalent variable substitution. `{{ name }}` markers resolve through a host-supplied dictionary; fenced and inline code regions pass through untouched. Optional HTML escaping; optional `Warning`-level logging for unresolved names. |
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

## Why pick NuStreamDocs

If you have a small docs site, every modern static-site generator works. The
cost shows up at scale — when you have thousands of pages, an editor that
runs your docs build on every save, or a CI job that pays for every minute
of build time. NuStreamDocs is built for that shape. Here's what's
different:

### Real numbers on a real corpus

Reference workload is the **ReactiveUI website**: 13,800 markdown files,
72 MB on disk — a corpus that mkdocs-material and Zensical both struggle
with. Recent BenchmarkDotNet runs on a typical workstation:

| Scenario | Time | Allocated |
|---|--:|--:|
| Baseline (parse + render + write) | **199 ms** | 46 MB |
| With markdown extensions | 358 ms | 90 MB |
| With nav generation | 544 ms | 229 MB |
| Full plugin stack | 1.2 s | 619 MB |

That's the **whole 13,800-page site, full pipeline, in just over a
second** — not per-page, total. Subsecond rebuilds for a one-file edit
fall out of incremental caching. For comparison, mkdocs-material on the
same corpus needs minutes for a cold build.

### What we did differently

- **UTF-8 in, UTF-8 out.** Markdown is read as bytes, parsed with
  byte-span scanners, emitted to `IBufferWriter<byte>` sinks. No
  per-token `string` allocations. A 10 MB document parses with
  `O(blocks)` heap allocations, not `O(bytes)`.
- **Per-page work is pure** and runs in parallel via
  `Parallel.ForEachAsync`. On a 16-core box you get 16-way speedup
  on the parse-render-emit phase by default.
- **Content-hash incremental builds.** Each input file is xxHash3'd
  into a manifest. An unchanged hash short-circuits parse + emit
  entirely — typical one-file edits rebuild in milliseconds.
- **Bounded, time-aware caches.** Parsed AST and search shards live in
  size-capped + age-capped LRUs keyed by content hash, so a long-running
  watch session doesn't grow without bound.
- **Pooled buffers everywhere.** Scanner blocks, HTML escapers, theme
  templates, icon SVG sinks — all rented from `ArrayPool<T>` or a
  per-pipeline `PageBuilderPool`. Per-page steady-state allocation on
  the rxui corpus is ~3 KB / page.
- **`SearchValues<byte>`** for delimiter scans — `IndexOfAny` runs
  vectorised on the actual SIMD path your CPU has.

### Native AOT-ready

The library assemblies build with `IsAotCompatible=true` and the trim
analyser enabled. Reflection-using dependencies (BenchmarkDotNet, Verify)
are quarantined to test + benchmark projects only. Your published binary
can be a single ~30 MB native executable that starts in milliseconds —
useful for CI containers and local pre-commit hooks where startup
overhead matters more than steady-state.

### Stable end-to-end memory profile

The 619 MB you see in the full-stack rxui benchmark is allocated during
the build, not retained — Gen 2 collections after build finish bring
working set back to ~80 MB. There's no per-page string interning, no
hidden global caches, no leaked task captures. Long-running watch
sessions stay flat.

### Reproducibility

Every plugin in the pipeline is byte-deterministic given the same input.
Same content + same plugins + same options = same output bytes — useful
for diffing, caching, and `git`-friendly site directories.

The `BenchmarkDotNet` harness under `src/benchmarks/` is what we run on
every perf-affecting change. Per-plugin benchmarks plus an end-to-end
rxui-corpus profile keep regressions visible.

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

- **[mkdocs-material](https://squidfunk.github.io/mkdocs-material/)** ([repo](https://github.com/squidfunk/mkdocs-material)) — MIT.
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
  behavioural reference for nav / search / blog / privacy plugins, and
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

- **[Material Design Icons (Pictogrammers)](https://pictogrammers.com/library/mdi/)** ([repo](https://github.com/Templarian/MaterialDesign-SVG)) — Pictogrammers Free License (icons under Apache 2.0).
  `NuStreamDocs.Icons.MaterialDesign` embeds the entire MDI catalogue
  (~7,400 icons) as inline-SVG path data baked into the assembly at
  build time so `:material-foo:` shortcodes match the markup
  mkdocs-material emits. Thanks to the Pictogrammers team for keeping
  this catalogue free, open source, and friendly to redistribute.

All six upstream projects ship under permissive licenses compatible
with this project's MIT license. Their license texts are reproduced
verbatim in [LICENSE](LICENSE) under the **Third-Party Notices**
section.

---

## License

MIT — see [LICENSE](LICENSE).
