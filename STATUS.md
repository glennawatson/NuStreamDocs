# NuStreamDocs — mkdocs/Material Parity Status

_Snapshot: 2026-04-29_

This is the working status doc: where we sit against mkdocs + Material +
pymdownx today, what's still missing, and what perf/infra work remains.
The README has the design pitch; this file is the punch list.

## Overall

- **41 of 43** tracked roadmap items complete.
- Walking skeleton through end-to-end build is functional: discover →
  parse → emit → write, with content-hash incremental cache, plugin
  pipeline, two themes (Material + Material3), and a CLI (`build`,
  `serve`, `watch`).
- ~all unit + integration test projects green at last green build (231
  tests passing as of the prior session — needs re-verify after the
  latest perf slice).

## Core engine — done

- UTF-8 span-based markdown parser + emitter (CommonMark blocks +
  inlines; `ReadOnlySpan<byte>` in, `IBufferWriter<byte>` out).
- Plugin contract (`IDocPlugin`, `IMarkdownPreprocessor`,
  `INavNeighboursProvider`, etc.) with `OnConfigure` / `OnRenderPage` /
  `OnFinalise` hooks.
- `DocBuilder` fluent config API + builder extension methods on every
  feature assembly (`UseApiGenerator`, `UseBlog`, `UseSearch`, …).
- Streaming pipeline over `IAsyncEnumerable<PageWorkItem>` with
  `Parallel.ForEachAsync`; per-page work is pure.
- `BoundedAstCache` — size + age capped LRU keyed on content hash.
- Content-hash incremental manifest (xxHash3) — short-circuits parse +
  emit on unchanged inputs.
- `PageBuilderPool` — thread-static multi-slot pool of
  `ArrayBufferWriter<byte>` rentals (recently extended to 4 slots).
- UTF-8 Mustache/Liquid-subset template engine (`NuStreamDocs.Templating`)
  with multi-pass iteration + partials.
- YAML→JSON span scanner for `mkdocs.yml`.
- Split config readers: `NuStreamDocs.Config.MkDocs` +
  `NuStreamDocs.Config.Zensical`.

## Themes — done

- `NuStreamDocs.Theme.Material` and `NuStreamDocs.Theme.Material3` —
  page templates wrap rendered HTML, embedded-vs-CDN asset source
  option, partials for search/toc/breadcrumbs, prev/next nav footer
  with section scoping, navigation.indexes, navigation.top,
  navigation.prune, toc.follow, scroll-to-top, repo_url + edit_uri.
- Icon assemblies: `NuStreamDocs.Icons.FontAwesome`,
  `NuStreamDocs.Icons.Material`.

## Plugins — done

| Plugin | Status |
|---|---|
| `NuStreamDocs.Nav` | nav builder + neighbours + prune |
| `NuStreamDocs.Autorefs` | cross-document `[ref][]` resolution |
| `NuStreamDocs.Search` | Pagefind default + Lunr-compatible alt |
| `NuStreamDocs.ApiGenerator` | C# reference generator |
| `NuStreamDocs.Versions` | mike-equivalent versioning |
| `NuStreamDocs.LinkValidator` | strict internal + Polly-throttled external |
| `NuStreamDocs.Lightbox` | glightbox parity |
| `NuStreamDocs.Blog` + `NuStreamDocs.Blog.MkDocs` | blog index, archive, categories, MkDocs-blog config compat |
| `NuStreamDocs.Feed` | RSS / Atom |
| `NuStreamDocs.Optimise` | image / asset optimisation (now uses .NET 10 async GZip) |
| `NuStreamDocs.Privacy` | external asset localisation, CSP hashes, URL globs, Polly retries — task #29 is functionally complete; tracker still flagged "pending" |
| `NuStreamDocs.Highlight` | server-side syntax highlight, Pygments-class taxonomy, GeneratedRegex grammars |

## Markdown extensions — pymdownx parity (#43, in progress)

### Done

- Admonitions (`!!! note`, `??? note` collapsed, `!!! note ""` titleless).
- Details (`???+`).
- Tabs (`=== "Title"`).
- Check-lists (`- [ ]` / `- [x]`).
- Mark (`==highlight==`).
- Caret/tilde (`^sup^`, `^^ins^^`, `~sub~`, `~~del~~`).
- Footnotes (`[^id]` + `[^id]: …`).
- Definition lists.
- Tables (GFM + alignment).
- Attr-list (block-level + inline).
- Smart-symbols (`(c)`, `(r)`, `(tm)`, arrows, fractions, `+/-`, `=/=`, `c/o`).
- Magic-link (bare `http(s)`, `ftp(s)`, `mailto:` URL autolinking).
- Keys (`++ctrl+alt+del++` → structured kbd span).
- Abbr (`*[token]: definition` → `<abbr title="…">` wraps).
- Arithmatex generic (`$x$` → `<span class="arithmatex">\(x\)</span>`, `$$x$$` → `<div>\[…\]</div>`).
- CriticMarkup (`{++ins++}`, `{--del--}`, `{~~old~>new~~}`, `{==hl==}`, `{>>cmt<<}`).
- Inlinehilite (`` `#!lang code` `` → `<code class="highlight language-lang">…</code>`).
- Emoji (`:name:` shortcodes → `<span class="twemoji">…</span>` via a built-in popular-shortcode index).
- Md_in_html (`<div markdown="1">…</div>` → blank-line-padded body so CommonMark parses the inside as Markdown).
- Icon shortcodes (`:material-foo:`, `:fontawesome-{style}-foo:`) — wired at the theme layer; Material classic renders Material-Icons-font ligature spans, Material3 renders Material-Symbols variable-font spans, both render FontAwesome `<i>` tags.

- SuperFences (`ICustomFenceHandler` dispatcher in `NuStreamDocs.SuperFences`).
- Snippets (`--8<-- "file"` whole-file includes via `NuStreamDocs.Snippets`).
- Betterem (intra-word `_` rejected; triple `***x***` renders as nested `<strong><em>`).

### Remaining polish

- **Snippets section markers.** Whole-file includes ship today; `--8<-- "file:section"` plus `--;--` / `--/8<--` block markers are not implemented.
- **Emoji index size.** Built-in dictionary covers ~80 popular shortcodes; full Twemoji set is ~1500 entries. Expansion is data-only — drop more entries into `EmojiIndex.BuildMap`.

### Site-level emitters — done

- Markdown link rewriting (`NuStreamDocs.Links.MarkdownLinkRewriterPlugin`): post-render pass that swaps relative `<a href="…/foo.md">` for `…/foo.html`, preserving anchors and queries; absolute URLs / `mailto:` / `tel:` left alone.
- Sitemap + robots.txt (`NuStreamDocs.Sitemap.SitemapPlugin`): collects URLs during the build and writes both files at finalise time. Requires `site_url` in config.
- Default 404 page (`NuStreamDocs.Sitemap.NotFoundPlugin`): emits a minimal `404.html` at the site root unless one already exists.
- Redirects (`NuStreamDocs.Sitemap.RedirectsPlugin`): meta-refresh stubs from caller-supplied `(from, to)` pairs.
- Mermaid now also implements `ICustomFenceHandler` so the SuperFences dispatcher can claim its blocks; the standalone `OnRenderPage` retag still works without SuperFences.

### Site-level emitters — not yet done

- **`use_directory_urls`** routing — outputs `foo/index.html` instead of `foo.html` and rewrites links to `foo/`. Requires both output-path and link-rewriter changes; landed neither yet.
- **`objects.inv`** Sphinx-style inventory — autorefs already does cross-doc resolution in-process, but emitting the inventory file lets external Sphinx sites cross-link in.

## Tooling — done

- **CLI** — `NuStreamDocs.Cli` with `build`, `serve`, `watch`,
  System.CommandLine 2.0.7, Kestrel preview server, strict-validation
  cache.
- **CI** — workflow + Dependabot.
- **Benchmarks** — BenchmarkDotNet harness covers every plugin
  assembly + end-to-end pipeline combos (`NuStreamDocs.Benchmarks`).

## What's left

### Roadmap items still pending

1. **#33 — benchmark suite vs the rxui corpus.** The harness exists,
   but we haven't wired the 13.8K-file / 72 MB rxui website corpus as
   a long-running stress profile. Needs: corpus-fetch step, a
   parameterised benchmark that runs cold + incremental, baseline
   numbers checked in.
2. **#43 — pymdownx parity.** See the missing-extensions list above.
3. **#29 — Privacy.** Code is shipped; just close the tracker entry
   once a fresh build/test pass confirms nothing regressed.

### Active perf work (carried over from this session)

The user listed three perf items; current state:

- ✅ **PageBuilderPool 4-slot extension** + `ApplyPreprocessors`
  pooled scratch — code complete in
  `src/NuStreamDocs/Building/PageBuilderPool.cs` and
  `BuildPipeline.cs`. **Build/tests not yet re-verified** after the
  background linter sweep — first thing to do next session.
- ⏳ **Theme `body = WrittenSpan.ToArray()`** — both
  `MaterialThemePlugin.OnRenderPage` and
  `Material3ThemePlugin.OnRenderPage` allocate a fresh `byte[]` per
  page for the rendered body before re-using `html` as the wrap
  output. Plan: refactor `TemplateData` to store
  `ReadOnlyMemory<byte>` instead of `byte[]`, then thread-static or
  pooled scratch buffer for the body copy. Side benefit: cache the
  per-build constants (`language`, `site_name`, `copyright`,
  `repo_url`, `head_extras`) as UTF-8 in `OnConfigure` instead of
  re-encoding per page (~8 ToUtf8 calls eliminated per page).
- ⏳ **Lexer per-rule scan loop.** `Lexer.StepOnce` runs every rule's
  `Regex.EnumerateMatches` per cursor position. Plan: bucket rules by
  their leading character (or `SearchValues<char>` of viable starts)
  and dispatch only the rules whose first byte can match the current
  cursor. Already using `EnumerateMatches` (allocation-free); this
  cuts the constant factor.

### Other backlog (not on the formal task list)

- **`md_in_html` + superfences dispatch shared with Mermaid.**
- **Inline-hilite (`#!lang …`).**
- **Critic / Keys / Caret-Tilde / Smartsymbols / Magiclink /
  Snippets / Arithmatex / Emoji.** (the pymdownx list above)
- **Watch-mode incremental nav rebuild.** Currently the watcher
  re-runs the full nav build on any change; nav is fast but on the
  rxui corpus this matters. Diff-based nav patch would help.
- **Public-API surface review for 1.0.** TemplateData byte[] →
  ReadOnlyMemory<byte> is the most disruptive pending API change;
  others to audit while still pre-1.0.
- **Publish to nuget.org.** Today consumers wire `artifacts/packages`
  as a local feed. Versions are computed by Nerdbank.GitVersioning;
  needs a release workflow that pushes tagged builds to nuget.org.
- **End-to-end render-smoke against a real Material site fixture.**
  We have plugin-level integration tests; we don't yet have a
  Material-rendered HTML golden fixture comparable to what
  `ZensicalRenderSmokeTests` does on the parser side.

## Immediate next actions (resume order)

1. Re-build + re-test after the PageBuilderPool slice
   (`dotnet build NuStreamDocs.slnx`, `dotnet test --solution
   NuStreamDocs.slnx`).
2. Land the theme-body refactor (TemplateData ReadOnlyMemory<byte> +
   constants cached in `OnConfigure`).
3. Land the lexer first-character dispatch.
4. Re-run the BenchmarkDotNet `[MemoryDiagnoser]` profiles to confirm
   per-page allocation drops and check the rxui-scale numbers.
5. Pick the next pymdownx extension off the list — superfences is the
   highest-leverage one because Mermaid + future custom fences depend
   on it.
