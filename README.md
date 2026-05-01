# NuStreamDocs

> **Status:** early scaffold — design doc + walking skeleton. Nothing is shippable yet.

A high-performance, AOT-friendly static-site generator for `.md` content. Reads
`mkdocs.yml`-style config, renders Markdown to UTF-8 HTML using a custom
span-based parser/emitter, and produces a Material/Zensical-styled site.

The default look mirrors [Zensical Material](https://github.com/zensical/zensical) /
[mkdocs-material](https://github.com/squidfunk/mkdocs-material). Theme assets
ship as embedded resources inside the generator assembly so a published build
is single-file and works under `dotnet publish -p:PublishAot=true`.

## Why another generator

We need three things existing tools don't combine:

1. **Native AOT, no reflection in the hot path.** Markdown parse, HTML emit
   and config read all run on `ReadOnlySpan<byte>` / `IBufferWriter<byte>`.
   Source-generated regex only where strictly needed.
2. **Tight integration with `SourceDocParser`** (sibling repo) for a real
   C# API reference, not a docfx-style sidecar.
3. **An mkdocs-plugin-equivalent feature set** out of the box, with a
   fluent C# builder instead of a YAML plugin list.

## Scale targets

Concrete reference workload: `~/source/rxui/website/docs` — **13.8K markdown
files, ~72 MB** of content. Both mkdocs-material and Zensical fall over on
sites this size (memory blow-up, single-threaded render, full rescan per
build). NuStreamDocs has to render this corpus in one process without
swapping, and ideally in seconds on incremental rebuilds.

Design rules that fall out of this:

- **Stream pages, never load the corpus at once.** A render is a pipeline
  of `IAsyncEnumerable<PageWorkItem>` stages — discover → parse → emit →
  write. Memory stays proportional to the active window, not the corpus.
- **Bounded, time-aware caches.** Parsed AST and search shards live in a
  size-capped LRU keyed by content hash; entries are evicted by both
  count and age so a long-running watcher doesn't accrete memory. No
  static `Dictionary` caches that outlive a build.
- **Per-page work is pure.** A page render only reads its own bytes plus
  immutable shared indexes (nav, autorefs catalog, search vocabulary).
  This makes the parse/emit stage trivially parallel via
  `Parallel.ForEachAsync` over the page enumerable.
- **Records / `record struct` for descriptors, not classes.** Block
  spans, page items, nav entries, search postings — all `readonly record
  struct` so they live on the stack or in pooled arrays. Reference-type
  records (`record class`) only when sharing identity matters
  (resolved cross-references, parsed config).
- **Content-hash incremental builds.** Each input file's xxHash3 is
  written to a manifest; an unchanged hash short-circuits parse + emit
  for that page. Targets sub-second rebuilds on a one-file edit even on
  the rxui corpus.
- **No per-page allocations of working buffers.** Scanner block buffers,
  escaper output buffers and HTML emitter buffers are rented from
  `ArrayPool<T>` / a `PageBuilderPool` (mirrors the pattern in
  `SourceDocParserLib`).

## Performance principles

- UTF-8 in, UTF-8 out — no `string` materialisation in parse or emit.
- All scanners take `ReadOnlySpan<byte>`; emitters write to
  `IBufferWriter<byte>` (typically a pooled `PipeWriter`).
- Block descriptors are 16-byte structs indexing the source buffer; a
  10MB doc parse is O(blocks) allocations, not O(bytes).
- `SearchValues<byte>` for delimiter scans; `IndexOfAny` runs vectorised.
- `IsAotCompatible=true` enforced on the production library; trim
  analyser is on. Reflection-using deps (BenchmarkDotNet) are quarantined
  in their own project.
- Source-generated regex is acceptable; `System.Text.RegularExpressions`
  at runtime without a generator is not.

## Engineering rules

The project's coding-style and engineering-rule digest lives in
[CONTRIBUTING.md](CONTRIBUTING.md). `CLAUDE.md` is the
machine-facing companion with hot-path detail.

## Repository layout

```
src/
  NuStreamDocs/                         core pipeline, renderer, templates, plugin contracts
  NuStreamDocs.Cli/                     AOT-published CLI driver
  NuStreamDocs.Config.MkDocs/           mkdocs-style YAML reader
  NuStreamDocs.Config.Zensical/         Zensical TOML reader
  NuStreamDocs.Theme.Material/          Material theme shell + embedded assets
  NuStreamDocs.Theme.Material3/         Material 3 theme shell + embedded assets
  NuStreamDocs.Nav/                     navigation tree/build/render plugin
  NuStreamDocs.Autorefs/                cross-page reference rewriting
  NuStreamDocs.Search/                  search-index generation
  NuStreamDocs.Privacy/                 external-asset localisation/privacy tooling
  NuStreamDocs.Blog/ + NuStreamDocs.Feed/ blog/archive/feed features
  NuStreamDocs.Highlight/ + NuStreamDocs.Mermaid/ + NuStreamDocs.Lightbox/ presentation plugins
  tests/                               TUnit + Verify project tests
  benchmarks/NuStreamDocs.Benchmarks/   BenchmarkDotNet
  Directory.Build.props
  Directory.Packages.props
  global.json
  testconfig.json
  NuStreamDocs.slnx
```

`SourceDocParser` is consumed as a NuGet package from
`/home/glennw/source/glennawatson/SourceDocParserLib` until a feed is wired up.

## Planned features

Each feature listed below is a target for v1. Items marked **[asm]** ship in
their own assembly so consumers can trim them out; the core library has no
dependency on them at runtime — registration is via the fluent builder.

### Content & navigation

- **Awesome-nav** — full mkdocs-awesome-nav-equivalent: glob includes,
  ordering hints, hidden sections, `.pages`-style overrides, multi-level
  rewrites. Goes well beyond Zensical's flat nav. Core feature.
- **autorefs** — cross-document `[symbol][]` resolution against the
  `SourceDocParser` catalog and any explicit anchor table. Core feature.
- **exclude** — glob-based content exclusion, evaluated up-front against
  `Microsoft.Extensions.FileSystemGlobbing`. Core feature.
- **C# API generation** — pulls `SourceDocParser`'s catalog and emits
  Material-styled type pages (members, inheritance, source-link). The
  `SourceDocParser` + Roslyn dependency lives **only** in this plugin
  so the core stays slim and AOT-clean. **[asm]**
  `NuStreamDocs.ApiGenerator`. Registered via `.UseApiGenerator(...)`.

### Search

- **Lunr-compatible client search** index built at generation time, with
  the index serialised as a UTF-8 JSON stream (no UTF-16 round trip).
  Stop-word lists and stemmer rules are source-generated so the index
  builder is reflection-free. Matches mkdocs-material / Zensical search
  behaviour. Core feature.

### Versioning & deployment

- **mike-equivalent** version aliasing — multi-version site builds, alias
  redirects, default-version selector. **[asm]** `NuStreamDocs.Versions`.
- **offline** — single-file-bundle mode (inlined CSS/JS, hashed assets,
  no CDN refs). Core feature.

### Media

- **glightbox** — `<a class="glightbox">` rewriting on image links plus
  the lightbox JS asset bundled as embedded resource. **[asm]**
  `NuStreamDocs.Lightbox`.

### Blog & syndication

- **Blog** — chronological post layout, tags, archive, paginated index.
  **[asm]** `NuStreamDocs.Blog`.
- **RSS / Atom** feeds, generated from blog posts and arbitrary content
  collections. UTF-8 `Utf8JsonWriter`-equivalent XML writer. **[asm]**
  `NuStreamDocs.Feed`.

### Privacy & optimisation

- **Privacy** — strips third-party requests, downloads referenced
  assets at build time, rewrites references. **[asm]**
  `NuStreamDocs.Privacy`.
- **Optimise** — image re-encoding, Brotli/Gzip pre-compression of
  emitted assets, HTML minification. **[asm]** `NuStreamDocs.Optimise`.

## Configuration model

Two equivalent surfaces:

- `mkdocs.yml` — for compatibility. Read via `JsonDocument` after a
  small embedded YAML→JSON convertor (no `YamlDotNet` dependency on the
  hot path).
- `DocBuilder` — fluent C# API. Recommended; type-safe, AOT-clean.

Plugin registration is generic and source-generated so the runtime never
calls `Activator.CreateInstance` or scans assemblies:

```csharp
var site = new DocBuilder()
    .WithTheme(Theme.Material)
    .UseAwesomeNav()
    .UseAutoRefs()
    .UseSearch(opts => opts.MinTokenLength = 3)
    .UsePlugin<BlogPlugin>(opts => opts.PostsDir = "posts")
    .UsePlugin<ApiPlugin>(opts => opts.AssemblyGlob = "**/*.dll")
    .UsePlugin<MikePlugin>()
    .UsePlugin<GLightboxPlugin>()
    .UsePlugin<PrivacyPlugin>()
    .UsePlugin<RssPlugin>()
    .UsePlugin<OptimisePlugin>()
    .Build();

await site.RenderAsync(input: "./docs", output: "./site");
```

`UsePlugin<T>` is the AOT seam: `T : IDocPlugin, new()` so registration is
a direct constructor call. Each plugin assembly publishes its public
options surface as `record` types parsable from either YAML or the
fluent builder.

## Testing & benchmarks

- **TUnit + Verify** under `src/tests`, run via Microsoft Testing Platform
  (`dotnet run --project ...Tests`). Coverage via
  `Microsoft.Testing.Extensions.CodeCoverage`.
- **BenchmarkDotNet** under `src/benchmarks`. Track allocations
  (`MemoryDiagnoser`) and IR (`DisassemblyDiagnoser` opt-in) for the
  scanner, escaper, search index builder.

## Open questions

- Theme licensing: lift Material CSS/JS verbatim (MIT, attribution
  preserved) or restyle from scratch? Default to verbatim with attribution
  in `Templates/THIRD_PARTY_NOTICES.md`.
- YAML scanner scope: hand-roll a minimal YAML→JSON path or reuse
  `YamlDotNet` only at the config read boundary.
- Whether the API generator gets its own emitter or feeds the same
  Markdown pipeline.
