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

A high-performance, Native AOT-ready .NET library for building static documentation sites from Markdown. Wire `DocBuilder` into a tiny C# console project, pick a theme and the plugins you want, and run `dotnet run` (or publish a single-file native executable).

Every parser, lexer, template engine, and HTML rewriter processes UTF-8 `ReadOnlySpan<byte>` buffers directly with zero regular expressions and zero runtime reflection.

## Documentation

Full guides and module references live in [`docs/`](docs/index.md):

- **[Documentation Hub (`docs/index.md`)](docs/index.md)**: Complete directory of every guide and module.
- **[Getting Started](docs/getting-started.md)**: Project setup, `DocBuilder` options, `"..."u8` configuration, custom plugins, and Native AOT publishing.
- **[Common Recipes](docs/recipes.md)**: Copy-paste setups for C# API docs, blogs, multi-version sites, privacy/CSP hardening, and live-reload dev servers.
- **[Performance Overview](docs/performance.md)** and **[Markdown Parity & Deviations](docs/markdown-deviations.md)**.

## Quick Start

```bash
dotnet new console -n MyDocs && cd MyDocs
dotnet add package NuStreamDocs
dotnet add package NuStreamDocs.Theme.Material3
dotnet add package NuStreamDocs.Nav
dotnet add package NuStreamDocs.Toc
dotnet add package NuStreamDocs.Highlight
dotnet add package NuStreamDocs.Search.Pagefind
dotnet add package NuStreamDocs.MarkdownExtensions
```

```csharp
using NuStreamDocs.Building;
using NuStreamDocs.Highlight;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Nav;
using NuStreamDocs.Search.Pagefind;
using NuStreamDocs.Theme.Material3;
using NuStreamDocs.Toc;

await new DocBuilder()
    .WithInput("docs")
    .WithOutput("site")
    .WithSiteName("My Project"u8)
    .WithSiteUrl("https://docs.example.com"u8)
    .UseDirectoryUrls()
    .UseMaterial3Theme()
    .UseNav(opts => opts with { Prune = true })
    .UseToc()
    .UseHighlight()
    .UsePagefindSearch()
    .UseCommonMarkdownExtensions()
    .BuildAsync();
```

## Modules

Each module ships as its own NuGet package so you only pull what you use. Click **Guide** on any row for full options, syntax, and output examples.

### Essentials & Themes

| Package | NuGet | Docs | Builder | Summary |
|---|---|---|---|---|
| [`NuStreamDocs`][Core] | [![ver][CoreV]][Core] | [Guide](docs/modules/core.md) | `new DocBuilder()` | Core UTF-8 Markdown compiler, HTML emitter, and plugin pipeline. |
| [`NuStreamDocs.Theme.Material3`][TM3] | [![ver][TM3V]][TM3] | [Guide](docs/modules/themes.md) | `.UseMaterial3Theme()` | Material Design 3 token-driven documentation theme. |
| [`NuStreamDocs.Theme.Material`][TM] | [![ver][TMV]][TM] | [Guide](docs/modules/themes.md) | `.UseMaterialTheme()` | Classic `mkdocs-material` documentation theme. |
| [`NuStreamDocs.Nav`][Nav] | [![ver][NavV]][Nav] | [Guide](docs/modules/nav-and-metadata.md) | `.UseNav()` | Sidebar navigation, `.pages` overrides, ordering, and orphan checks. |
| [`NuStreamDocs.Toc`][Toc] | [![ver][TocV]][Toc] | [Guide](docs/modules/toc-and-tags.md) | `.UseToc()` | Per-page table of contents and heading permalink anchors. |
| [`NuStreamDocs.Highlight`][Highlight] | [![ver][HighlightV]][Highlight] | [Guide](docs/modules/highlight.md) | `.UseHighlight()` | Server-side UTF-8 syntax highlighting, line numbers, `hl_lines`, `[!code ++/--/focus]`, callouts, and copy buttons. |
| [`NuStreamDocs.MarkdownExtensions`][MdExt] | [![ver][MdExtV]][MdExt] | [Guide](docs/modules/markdown-extensions.md) | `.UseCommonMarkdownExtensions()` | Admonitions, tabs, details, tables, footnotes, checklists, abbreviations, and CriticMarkup. |

### Search

| Package | NuGet | Docs | Builder | Summary |
|---|---|---|---|---|
| [`NuStreamDocs.Search.Pagefind`][Pagefind] | [![ver][PagefindV]][Pagefind] | [Guide](docs/modules/search.md) | `.UsePagefindSearch()` | Sharded WASM search index with snippets via bundled Pagefind CLI. |
| [`NuStreamDocs.Search.Lunr`][Lunr] | [![ver][LunrV]][Lunr] | [Guide](docs/modules/search.md) | `.UseLunrSearch()` | Pure-JS Lunr-compatible JSON search index. |
| [`NuStreamDocs.Search.Sqlite`][SqliteSearch] | [![ver][SqliteSearchV]][SqliteSearch] | [Guide](docs/modules/search.md) | `.UseSqliteSearch()` | Single-file `search.db` (SQLite FTS5 over HTTP Range requests) for large sites. |
| [`NuStreamDocs.Search`][Search] | [![ver][SearchV]][Search] | [Guide](docs/modules/search.md) | `.UseSearch()` | Shared search base and section-priority ranking metadata. |

### Authoring & Markdown Syntax

| Package | NuGet | Docs | Builder | Summary |
|---|---|---|---|---|
| [`NuStreamDocs.Snippets`][Snip] | [![ver][SnipV]][Snip] | [Guide](docs/modules/snippets-macros-layouts.md) | `.UseSnippets()` | `--8<-- "file#section"` whole-file and section includes. |
| [`NuStreamDocs.Macros`][Macros] | [![ver][MacrosV]][Macros] | [Guide](docs/modules/snippets-macros-layouts.md) | `.UseMacros()` | `{{ name }}` dictionary variable substitution outside code spans. |
| [`NuStreamDocs.GitInfo`][GitInfo] | [![ver][GitInfoV]][GitInfo] | [Guide](docs/modules/git-info.md) | `.UseGitInfo()` | Batched `git log` revision dates, contributor footers, and `sitemap.xml` `<lastmod>` enrichment. |
| [`NuStreamDocs.Layouts`][Layouts] | [![ver][LayoutsV]][Layouts] | [Guide](docs/modules/snippets-macros-layouts.md) | `.UseLayouts()` | Jinja-style `{% extends %}` / `{% block %}` Markdown template inheritance. |
| [`NuStreamDocs.Bibliography`][Bib] | [![ver][BibV]][Bib] | [Guide](docs/modules/bibliography.md) | `.UseBibliography()` | Pandoc `[@key]` citations, CSL-JSON loader, and AGLC4 formatting. |
| [`NuStreamDocs.SuperFences`][SF] | [![ver][SFV]][SF] | [Guide](docs/modules/highlight.md) | `.UseSuperFences()` | Custom fenced-block dispatcher for diagram and widget plugins. |
| [`NuStreamDocs.Arithmatex`][Arith] | [![ver][ArithV]][Arith] | [Guide](docs/modules/inline-extensions.md) | `.UseArithmatex()` | `$x$` and `$$x$$` TeX math markup. |
| [`NuStreamDocs.Arithmatex.MathJax`][ArithMJ] | [![ver][ArithMJV]][ArithMJ] | [Guide](docs/modules/inline-extensions.md) | `.UseMathJax()` | Client-side MathJax 3 runtime injection. |
| [`NuStreamDocs.Emoji`][Emoji] | [![ver][EmojiV]][Emoji] | [Guide](docs/modules/inline-extensions.md) | `.UseEmoji()` | `:shortcode:` Twemoji spans. |
| [`NuStreamDocs.Keys`][Keys] | [![ver][KeysV]][Keys] | [Guide](docs/modules/inline-extensions.md) | `.UseKeys()` | `++ctrl+alt+del++` keyboard chord markup. |
| [`NuStreamDocs.SmartSymbols`][Smart] | [![ver][SmartV]][Smart] | [Guide](docs/modules/inline-extensions.md) | `.UseSmartSymbols()` | Typographic arrows, fractions, and `(c)` / `(tm)` symbols. |
| [`NuStreamDocs.Glossary`][Gloss] | [![ver][GlossV]][Gloss] | [Guide](docs/modules/glossary.md) | `.UseGlossary()` | Central glossary page generator and accessible `<abbr>` term tooltips. |
| [`NuStreamDocs.MagicLink`][Magic] | [![ver][MagicV]][Magic] | [Guide](docs/modules/inline-extensions.md) | `.UseMagicLink()` | Bare URL and email autolinking. |

### Media & Icons

| Package | NuGet | Docs | Builder | Summary |
|---|---|---|---|---|
| [`NuStreamDocs.Videos`][Vid] | [![ver][VidV]][Vid] | [Guide](docs/modules/videos.md) | `.UseVideos()` | Responsive embeds for YouTube, Vimeo, OneDrive, SharePoint/Stream, Azure, Loom, Dailymotion, Twitch, Wistia, Cloudflare, Google Drive, Bilibili, Vidyard, and HTML5 video. |
| [`NuStreamDocs.Mermaid`][Mer] | [![ver][MerV]][Mer] | [Guide](docs/modules/mermaid-and-lightbox.md) | `.UseMermaid()` | Fenced `mermaid` diagram rendering. |
| [`NuStreamDocs.Lightbox`][LB] | [![ver][LBV]][LB] | [Guide](docs/modules/mermaid-and-lightbox.md) | `.UseLightbox()` | `glightbox` zoomable content images. |
| [`NuStreamDocs.Images`][Images] | [![ver][ImagesV]][Images] | [Guide](docs/modules/images.md) | `.UseImages()` | Zero-CLS PNG/JPEG/GIF/WebP/AVIF/SVG dimension reader, `loading="lazy"`, `decoding="async"`, and `<figure>` captions. |
| [`NuStreamDocs.Icons.MaterialDesign`][IcMD] | [![ver][IcMDV]][IcMD] | [Guide](docs/modules/icons.md) | `new MdiIconResolver()` | Inline SVG Pictogrammers Material Design Icons (~7,400 icons). |
| [`NuStreamDocs.Icons.Material`][IcM] | [![ver][IcMV]][IcM] | [Guide](docs/modules/icons.md) | `.UseMaterialIcons()` | Google Material Icons / Symbols stylesheet link. |
| [`NuStreamDocs.Icons.FontAwesome`][IcFA] | [![ver][IcFAV]][IcFA] | [Guide](docs/modules/icons.md) | `.UseFontAwesome()` | Font Awesome Free stylesheet link. |

### Content Sources, API Reference & Blogs

| Package | NuGet | Docs | Builder | Summary |
|---|---|---|---|---|
| [`NuStreamDocs.CSharpApiGenerator`][Api] | [![ver][ApiV]][Api] | [Guide](docs/modules/csharp-api-generator.md) | `.UseCSharpApiGenerator()` | Generates C# API reference pages from NuGet packages or DLLs. |
| [`NuStreamDocs.ContentLoader`][CL] | [![ver][CLV]][CL] | [Guide](docs/modules/content-loaders.md) | `.UseContentLoaders(...)` | Synthesizes pages from JSON/YAML files, REST/GraphQL endpoints, and raw URLs. |
| [`NuStreamDocs.ContentLoader.OpenApi`][CLOAS] | [![ver][CLOASV]][CLOAS] | [Guide](docs/modules/content-loaders.md) | `new OpenApiContentLoader(...)` | Generates REST reference pages from OpenAPI 3.x specs. |
| [`NuStreamDocs.ContentLoader.Schemas`][CLSchemas] | [![ver][CLSchemasV]][CLSchemas] | [Guide](docs/modules/content-loader-schemas.md) | `new JsonSchemaContentLoader(...)` | Generates reference pages from JSON Schema, GraphQL SDL, and AsyncAPI specs. |
| [`NuStreamDocs.ContentLoader.GitHub`][CLGH] | [![ver][CLGHV]][CLGH] | [Guide](docs/modules/content-loaders.md) | `new GitHubContentLoader(...)` | Pulls remote repo Markdown trees and GitHub Releases changelogs. |
| [`NuStreamDocs.ContentLoader.Feed`][CLFeed] | [![ver][CLFeedV]][CLFeed] | [Guide](docs/modules/content-loaders.md) | `new FeedContentLoader(...)` | Imports external RSS/Atom items as documentation pages. |
| [`NuStreamDocs.Blog.MkDocs`][BlogMk] | [![ver][BlogMkV]][BlogMk] | [Guide](docs/modules/blog-and-feed.md) | `.UseMkDocsBlog()` | `mkdocs-material`-flavored `blog/posts/` index and category archives. |
| [`NuStreamDocs.Blog`][Blog] | [![ver][BlogV]][Blog] | [Guide](docs/modules/blog-and-feed.md) | `.UseWyamBlog()` | Wyam-flavored `YYYY-MM-DD-slug.md` blog and tag archives. |
| [`NuStreamDocs.Feed`][Feed] | [![ver][FeedV]][Feed] | [Guide](docs/modules/blog-and-feed.md) | `.UseFeed()` | RSS 2.0 and Atom 1.0 feed generation. |
| [`NuStreamDocs.Export`][Exp] | [![ver][ExpV]][Exp] | [Guide](docs/modules/export.md) | `.UseExport()` | Consolidates all chapters into a standalone print-ready and PDF-ready `/print.html` bundle. |

### Site Emitters, Config Readers, Security & Dev Experience

| Package | NuGet | Docs | Builder | Summary |
|---|---|---|---|---|
| [`NuStreamDocs.Sitemap`][Site] | [![ver][SiteV]][Site] | [Guide](docs/modules/sitemap-and-redirects.md) | `.UseSitemap()`, `.UseNotFoundPage()` | Emits `sitemap.xml`, `robots.txt`, and themed `404.html`. |
| [`NuStreamDocs.Redirects`][Redir] | [![ver][RedirV]][Redir] | [Guide](docs/modules/sitemap-and-redirects.md) | `.UseRedirects(...)` | Emits `_redirects`, `_headers`, and meta-refresh HTML stubs from `redirect_from`. |
| [`NuStreamDocs.I18n`][I18n] | [![ver][I18nV]][I18n] | [Guide](docs/modules/i18n.md) | `.UseI18n(...)` | Multi-locale routing, fallback synthesis, `hreflang` head and `sitemap.xml` links, and language switcher. |
| [`NuStreamDocs.Versions`][Ver] | [![ver][VerV]][Ver] | [Guide](docs/modules/versions.md) | `.UseVersions()` | `mike`-compatible `versions.json` multi-release manifest. |
| [`NuStreamDocs.Tags`][Tags] | [![ver][TagsV]][Tags] | [Guide](docs/modules/toc-and-tags.md) | `.UseTags()` | Tag index and per-tag listing pages from `tags:` frontmatter. |
| [`NuStreamDocs.Metadata`][Meta] | [![ver][MetaV]][Meta] | [Guide](docs/modules/nav-and-metadata.md) | `.UseMetadata()` | Directory `_meta.yml` and `page.meta.yml` frontmatter inheritance. |
| [`NuStreamDocs.Autorefs`][Auto] | [![ver][AutoV]][Auto] | [Guide](docs/modules/cross-references.md) | `.UseAutorefs()` | Cross-page `@autoref:ID` heading and UID resolution. |
| [`NuStreamDocs.Xrefs`][Xref] | [![ver][XrefV]][Xref] | [Guide](docs/modules/cross-references.md) | `.UseXrefs()` | DocFX `xrefmap.json` emission and external xrefmap import. |
| [`NuStreamDocs.SphinxInventory`][Sphinx] | [![ver][SphinxV]][Sphinx] | [Guide](docs/modules/cross-references.md) | `.UseSphinxInventory()` | Sphinx v2 `objects.inv` intersphinx emitter. |
| [`NuStreamDocs.Llms`][Llms] | [![ver][LlmsV]][Llms] | [Guide](docs/modules/llms.md) | `.UseLlms()` | Emits `/llms.txt`, `/llms-full.txt`, and clean `.md` page siblings for AI coding assistants. |
| [`NuStreamDocs.Privacy`][Priv] | [![ver][PrivV]][Priv] | [Guide](docs/modules/privacy-csp-fonts.md) | `.UsePrivacy()` | Downloads third-party assets into `assets/external/` at build time. |
| [`NuStreamDocs.Csp`][Csp] | [![ver][CspV]][Csp] | [Guide](docs/modules/privacy-csp-fonts.md) | `.UseCsp(...)` | Per-page SHA-256 hashed `Content-Security-Policy` meta injection. |
| [`NuStreamDocs.Fonts`][Fonts] | [![ver][FontsV]][Fonts] | [Guide](docs/modules/privacy-csp-fonts.md) | `.UseFonts(...)` | Build-time `.woff2` font self-hosting with zero-CLS fallback metrics. |
| [`NuStreamDocs.Optimize`][Opt] | [![ver][OptV]][Opt] | [Guide](docs/modules/optimize-audit-validator.md) | `.UseOptimize()`, `.UseHtmlMinify()` | HTML minification and pre-compressed `.gz` / `.br` sibling assets. |
| [`NuStreamDocs.Audit`][Audit] | [![ver][AuditV]][Audit] | [Guide](docs/modules/optimize-audit-validator.md) | `.UseAudit(...)` | Build-time accessibility and HTML quality linter. |
| [`NuStreamDocs.LinkValidator`][LV] | [![ver][LVV]][LV] | [Guide](docs/modules/optimize-audit-validator.md) | `.UseLinkValidator()` | Strict internal anchor/path checker and throttled external URL validator. |
| [`NuStreamDocs.Transitions`][Trans] | [![ver][TransV]][Trans] | [Guide](docs/modules/transitions-and-serve.md) | `.UseTransitions(...)` | View Transitions soft router with hover prefetching. |
| [`NuStreamDocs.Serve`][Serve] | [![ver][ServeV]][Serve] | [Guide](docs/modules/transitions-and-serve.md) | `.WatchAndServeAsync()` | Debounced file watcher, Kestrel static server, and WebSocket LiveReload. |
| [`NuStreamDocs.Social`][Social] | [![ver][SocialV]][Social] | [Guide](docs/modules/social.md) | `.UseSocial()` | OpenGraph, Twitter/X cards, canonical URLs, Schema.org JSON-LD, and `1200x630` SVG social preview cards. |
| [`NuStreamDocs.Config.MkDocs`][Cfg1] | [![ver][Cfg1V]][Cfg1] | [Guide](docs/modules/configuration-readers.md) | `.UseMkDocsConfig()` | Span-based `mkdocs.yml` configuration reader. |
| [`NuStreamDocs.Config.Zensical`][Cfg2] | [![ver][Cfg2V]][Cfg2] | [Guide](docs/modules/configuration-readers.md) | `.UseZensicalConfig()` | Span-based `zensical.toml` configuration reader. |
| [`NuStreamDocs.Config.DocFx`][CfgDfx] | [![ver][CfgDfxV]][CfgDfx] | [Guide](docs/modules/configuration-readers.md) | `.WithDocFxToc()` | DocFX `toc.yml` navigation reader. |

*(Shared plumbing packages pulled transitively: [`NuStreamDocs.Common`][Cm], [`NuStreamDocs.Markdown.Common`][Mc], [`NuStreamDocs.Templating`][Tp], [`NuStreamDocs.Theme.Common`][Tc], and [`NuStreamDocs.Blog.Common`][Bc].)*

## Testing & Contributing

```bash
cd src
dotnet test --solution NuStreamDocs.slnx
```

Read [CONTRIBUTING.md](CONTRIBUTING.md) and [CLAUDE.md](CLAUDE.md) before opening a pull request.

## License

MIT. See [LICENSE](LICENSE) and [Acknowledgements & Third-Party Credits](docs/acknowledgements.md).

[Core]: https://www.nuget.org/packages/NuStreamDocs/
[CoreV]: https://img.shields.io/nuget/v/NuStreamDocs.svg?label=
[I18n]: https://www.nuget.org/packages/NuStreamDocs.I18n/
[I18nV]: https://img.shields.io/nuget/v/NuStreamDocs.I18n.svg?label=
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
[CLSchemas]: https://www.nuget.org/packages/NuStreamDocs.ContentLoader.Schemas/
[CLSchemasV]: https://img.shields.io/nuget/v/NuStreamDocs.ContentLoader.Schemas.svg?label=
[TM3]: https://www.nuget.org/packages/NuStreamDocs.Theme.Material3/
[TM3V]: https://img.shields.io/nuget/v/NuStreamDocs.Theme.Material3.svg?label=
[Cfg1]: https://www.nuget.org/packages/NuStreamDocs.Config.MkDocs/
[Cfg1V]: https://img.shields.io/nuget/v/NuStreamDocs.Config.MkDocs.svg?label=
[Cfg2]: https://www.nuget.org/packages/NuStreamDocs.Config.Zensical/
[Cfg2V]: https://img.shields.io/nuget/v/NuStreamDocs.Config.Zensical.svg?label=
[CfgDfx]: https://www.nuget.org/packages/NuStreamDocs.Config.DocFx/
[CfgDfxV]: https://img.shields.io/nuget/v/NuStreamDocs.Config.DocFx.svg?label=
[Nav]: https://www.nuget.org/packages/NuStreamDocs.Nav/
[NavV]: https://img.shields.io/nuget/v/NuStreamDocs.Nav.svg?label=
[Toc]: https://www.nuget.org/packages/NuStreamDocs.Toc/
[TocV]: https://img.shields.io/nuget/v/NuStreamDocs.Toc.svg?label=
[Gloss]: https://www.nuget.org/packages/NuStreamDocs.Glossary/
[GlossV]: https://img.shields.io/nuget/v/NuStreamDocs.Glossary.svg?label=
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
[GitInfo]: https://www.nuget.org/packages/NuStreamDocs.GitInfo/
[GitInfoV]: https://img.shields.io/nuget/v/NuStreamDocs.GitInfo.svg?label=
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
[Llms]: https://www.nuget.org/packages/NuStreamDocs.Llms/
[LlmsV]: https://img.shields.io/nuget/v/NuStreamDocs.Llms.svg?label=
[Layouts]: https://www.nuget.org/packages/NuStreamDocs.Layouts/
[LayoutsV]: https://img.shields.io/nuget/v/NuStreamDocs.Layouts.svg?label=
[LV]: https://www.nuget.org/packages/NuStreamDocs.LinkValidator/
[LVV]: https://img.shields.io/nuget/v/NuStreamDocs.LinkValidator.svg?label=
[Serve]: https://www.nuget.org/packages/NuStreamDocs.Serve/
[ServeV]: https://img.shields.io/nuget/v/NuStreamDocs.Serve.svg?label=
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
[Images]: https://www.nuget.org/packages/NuStreamDocs.Images/
[ImagesV]: https://img.shields.io/nuget/v/NuStreamDocs.Images.svg?label=
[Vid]: https://www.nuget.org/packages/NuStreamDocs.Videos/
[VidV]: https://img.shields.io/nuget/v/NuStreamDocs.Videos.svg?label=
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
[Exp]: https://www.nuget.org/packages/NuStreamDocs.Export/
[ExpV]: https://img.shields.io/nuget/v/NuStreamDocs.Export.svg?label=
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
[Social]: https://www.nuget.org/packages/NuStreamDocs.Social/
[SocialV]: https://img.shields.io/nuget/v/NuStreamDocs.Social.svg?label=
[Api]: https://www.nuget.org/packages/NuStreamDocs.CSharpApiGenerator/
[ApiV]: https://img.shields.io/nuget/v/NuStreamDocs.CSharpApiGenerator.svg?label=
[Bc]: https://www.nuget.org/packages/NuStreamDocs.Blog.Common/
