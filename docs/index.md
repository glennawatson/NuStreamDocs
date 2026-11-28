# NuStreamDocs Documentation

Welcome to the detailed module and authoring reference for **NuStreamDocs**. Every feature in NuStreamDocs ships as a focused, Native AOT-friendly NuGet package wired onto `DocBuilder`.

## Architecture & Core Guides

- [Core Engine & Plugin Pipeline](modules/core.md): `NuStreamDocs`, `NuStreamDocs.Common`, `NuStreamDocs.Markdown.Common`, `NuStreamDocs.Templating`, `DocBuilder` options, build phases, and writing custom plugins.
- [Themes (`Material` and `Material3`)](modules/themes.md): `NuStreamDocs.Theme.Material`, `NuStreamDocs.Theme.Material3`, and `NuStreamDocs.Theme.Common`.
- [Configuration Readers (`mkdocs.yml`, `zensical.toml`, `docfx.json`)](modules/configuration-readers.md): `NuStreamDocs.Config.MkDocs`, `NuStreamDocs.Config.Zensical`, and `NuStreamDocs.Config.DocFx`.
- [Markdown Deviations](markdown-deviations.md): Exact CommonMark, MkDocs, and Zensical parity notes.
- [Performance Overview](performance.md): Byte-first UTF-8 design and benchmark methodology.

## Navigation, Search & Cross-References

- [Navigation & Page Metadata](modules/nav-and-metadata.md): `NuStreamDocs.Nav` and `NuStreamDocs.Metadata` (`.pages`, `_meta.yml`, sidecars, ordering, and hiding pages).
- [Table of Contents & Tags](modules/toc-and-tags.md): `NuStreamDocs.Toc` and `NuStreamDocs.Tags`.
- [Cross-References (`Autorefs`, `Xrefs`, `SphinxInventory`)](modules/cross-references.md): `NuStreamDocs.Autorefs`, `NuStreamDocs.Xrefs`, and `NuStreamDocs.SphinxInventory`.
- [Search (`Pagefind`, `Lunr`, `SQLite`)](modules/search.md): `NuStreamDocs.Search`, `NuStreamDocs.Search.Pagefind`, `NuStreamDocs.Search.Lunr`, and `NuStreamDocs.Search.Sqlite`.
- [Versioning (`versions.json`)](modules/versions.md): `NuStreamDocs.Versions`.

## Markdown Authoring & Code

- [Syntax Highlighting](modules/highlight.md): `NuStreamDocs.Highlight` and `NuStreamDocs.SuperFences`.
- [Markdown Extensions](modules/markdown-extensions.md): `NuStreamDocs.MarkdownExtensions` (admonitions, content tabs, details, footnotes, checklists, tables, abbreviations, definition lists, attr-list, CriticMarkup).
- [Snippets, Macros & Layouts](modules/snippets-macros-layouts.md): `NuStreamDocs.Snippets`, `NuStreamDocs.Macros`, and `NuStreamDocs.Layouts`.
- [Bibliography & Citations](modules/bibliography.md): `NuStreamDocs.Bibliography` (AGLC4 and CSL-JSON citations).
- [Math, Emoji, Keys, Symbols & MagicLink](modules/inline-extensions.md): `NuStreamDocs.Arithmatex`, `NuStreamDocs.Arithmatex.MathJax`, `NuStreamDocs.Emoji`, `NuStreamDocs.Keys`, `NuStreamDocs.SmartSymbols`, and `NuStreamDocs.MagicLink`.

## Media & Icons

- [Embedded Videos](modules/videos.md): `NuStreamDocs.Videos` (YouTube, Vimeo, OneDrive, SharePoint/Stream, Azure, Loom, Dailymotion, Twitch, Wistia, Cloudflare Stream, Google Drive, Bilibili, Vidyard, and HTML5 video files).
- [Mermaid Diagrams & Image Lightbox](modules/mermaid-and-lightbox.md): `NuStreamDocs.Mermaid` and `NuStreamDocs.Lightbox`.
- [Icon Packs](modules/icons.md): `NuStreamDocs.Icons.MaterialDesign`, `NuStreamDocs.Icons.Material`, and `NuStreamDocs.Icons.FontAwesome`.

## External Content, API Docs & Blogs

- [Content Loaders (`File`, `HTTP`, `Feed`, `GitHub`, `OpenAPI`)](modules/content-loaders.md): `NuStreamDocs.ContentLoader`, `NuStreamDocs.ContentLoader.Feed`, `NuStreamDocs.ContentLoader.GitHub`, and `NuStreamDocs.ContentLoader.OpenApi`.
- [C# API Reference Generator](modules/csharp-api-generator.md): `NuStreamDocs.CSharpApiGenerator`.
- [Blogs & RSS/Atom Feeds](modules/blog-and-feed.md): `NuStreamDocs.Blog`, `NuStreamDocs.Blog.MkDocs`, and `NuStreamDocs.Feed`.

## Site Emitters, Security, Quality & Dev Server

- [Sitemap, 404 & Redirects](modules/sitemap-and-redirects.md): `NuStreamDocs.Sitemap` and `NuStreamDocs.Redirects`.
- [Privacy, Content Security Policy & Self-Hosted Fonts](modules/privacy-csp-fonts.md): `NuStreamDocs.Privacy`, `NuStreamDocs.Csp`, and `NuStreamDocs.Fonts`.
- [Optimization, HTML Audit & Link Validation](modules/optimize-audit-validator.md): `NuStreamDocs.Optimize`, `NuStreamDocs.Audit`, and `NuStreamDocs.LinkValidator`.
- [View Transitions & Live-Reload Dev Server](modules/transitions-and-serve.md): `NuStreamDocs.Transitions` and `NuStreamDocs.Serve`.
