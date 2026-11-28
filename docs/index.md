# NuStreamDocs Documentation

Welcome to the complete user and module documentation for **NuStreamDocs**.

## Start Here

- [Getting Started](getting-started.md): Creating a project, `DocBuilder` configuration, `"..."u8` literals, and Native AOT publishing.
- [Common Recipes](recipes.md): Ready-to-run `Program.cs` configurations for API docs, blogs, versioned docs, privacy-hardened sites, and live-reload servers.
- [Performance Overview](performance.md): Byte-first UTF-8 architecture and benchmark methodology.
- [Markdown Deviations](markdown-deviations.md): Exact CommonMark, MkDocs, and Zensical parity rules.
- [Acknowledgements](acknowledgements.md): Upstream open-source credits and license notices.

## Module Reference Guides

### Core, Themes & Configuration
- [Core Engine & Plugin Pipeline](modules/core.md): `NuStreamDocs`, `NuStreamDocs.Common`, `NuStreamDocs.Markdown.Common`, `NuStreamDocs.Templating`
- [Themes (`Material` and `Material3`)](modules/themes.md): `NuStreamDocs.Theme.Material`, `NuStreamDocs.Theme.Material3`, `NuStreamDocs.Theme.Common`
- [Configuration Readers](modules/configuration-readers.md): `NuStreamDocs.Config.MkDocs`, `NuStreamDocs.Config.Zensical`, `NuStreamDocs.Config.DocFx`
- [Internationalization & Multi-Locale Routing](modules/i18n.md): `NuStreamDocs.I18n`

### Navigation, Search & Cross-References
- [Navigation & Page Metadata](modules/nav-and-metadata.md): `NuStreamDocs.Nav`, `NuStreamDocs.Metadata`
- [Schema Loaders (`JSON Schema`, `GraphQL`, `AsyncAPI`)](modules/content-loader-schemas.md): `NuStreamDocs.ContentLoader.Schemas`
- [Table of Contents & Tags](modules/toc-and-tags.md): `NuStreamDocs.Toc`, `NuStreamDocs.Tags`
- [Cross-References (`Autorefs`, `Xrefs`, `SphinxInventory`)](modules/cross-references.md): `NuStreamDocs.Autorefs`, `NuStreamDocs.Xrefs`, `NuStreamDocs.SphinxInventory`
- [Search (`Pagefind`, `Lunr`, `SQLite`)](modules/search.md): `NuStreamDocs.Search`, `NuStreamDocs.Search.Pagefind`, `NuStreamDocs.Search.Lunr`, `NuStreamDocs.Search.Sqlite`
- [Versioning (`versions.json`)](modules/versions.md): `NuStreamDocs.Versions`
- [Git Revision Dates & Contributors](modules/git-info.md): `NuStreamDocs.GitInfo`

### Markdown Authoring & Code
- [Syntax Highlighting & SuperFences](modules/highlight.md): `NuStreamDocs.Highlight`, `NuStreamDocs.SuperFences`
- [Markdown Extensions](modules/markdown-extensions.md): `NuStreamDocs.MarkdownExtensions`
- [Snippets, Macros & Layouts](modules/snippets-macros-layouts.md): `NuStreamDocs.Snippets`, `NuStreamDocs.Macros`, `NuStreamDocs.Layouts`
- [Bibliography & Citations](modules/bibliography.md): `NuStreamDocs.Bibliography`
- [Math, Emoji, Keys, SmartSymbols & MagicLink](modules/inline-extensions.md): `NuStreamDocs.Arithmatex`, `NuStreamDocs.Arithmatex.MathJax`, `NuStreamDocs.Emoji`, `NuStreamDocs.Keys`, `NuStreamDocs.SmartSymbols`, `NuStreamDocs.MagicLink`

### Media & Icons
- [Embedded Videos](modules/videos.md): `NuStreamDocs.Videos`
- [Mermaid Diagrams & Image Lightbox](modules/mermaid-and-lightbox.md): `NuStreamDocs.Mermaid`, `NuStreamDocs.Lightbox`
- [Responsive Images, Dimensions & Figures](modules/images.md): `NuStreamDocs.Images`
- [Icon Packs](modules/icons.md): `NuStreamDocs.Icons.MaterialDesign`, `NuStreamDocs.Icons.Material`, `NuStreamDocs.Icons.FontAwesome`

### External Content, API Docs & Blogs
- [Content Loaders (`File`, `HTTP`, `Feed`, `GitHub`, `OpenAPI`)](modules/content-loaders.md): `NuStreamDocs.ContentLoader`, `NuStreamDocs.ContentLoader.Feed`, `NuStreamDocs.ContentLoader.GitHub`, `NuStreamDocs.ContentLoader.OpenApi`
- [C# API Reference Generator](modules/csharp-api-generator.md): `NuStreamDocs.CSharpApiGenerator`
- [Social Cards, OpenGraph & JSON-LD](modules/social.md): `NuStreamDocs.Social`
- [Blogs & RSS/Atom Feeds](modules/blog-and-feed.md): `NuStreamDocs.Blog`, `NuStreamDocs.Blog.MkDocs`, `NuStreamDocs.Feed`

### Site Emitters, Security, Quality & Dev Server
- [Sitemap, 404 & Redirects](modules/sitemap-and-redirects.md): `NuStreamDocs.Sitemap`, `NuStreamDocs.Redirects`
- [AI & LLM Endpoints (`llms.txt`)](modules/llms.md): `NuStreamDocs.Llms`
- [Privacy, CSP & Self-Hosted Fonts](modules/privacy-csp-fonts.md): `NuStreamDocs.Privacy`, `NuStreamDocs.Csp`, `NuStreamDocs.Fonts`
- [Optimization, HTML Audit & Link Validation](modules/optimize-audit-validator.md): `NuStreamDocs.Optimize`, `NuStreamDocs.Audit`, `NuStreamDocs.LinkValidator`
- [View Transitions & Live-Reload Dev Server](modules/transitions-and-serve.md): `NuStreamDocs.Transitions`, `NuStreamDocs.Serve`
