# Acknowledgements & Third-Party Credits

NuStreamDocs builds on ideas and permissively licensed assets from several open-source projects:

- **[mkdocs-material](https://squidfunk.github.io/mkdocs-material/)** ([repo](https://github.com/squidfunk/mkdocs-material), MIT): Visual baseline, page template conventions, CSS class names, and icon shortcode patterns.
- **[MkDocs](https://www.mkdocs.org/)** ([repo](https://github.com/mkdocs/mkdocs), BSD-2-Clause): Navigation model and `mkdocs.yml` configuration schema read by `NuStreamDocs.Config.MkDocs`.
- **[Zensical](https://zensical.org/)** ([repo](https://github.com/zensical/zensical), MIT): Behavioral reference for navigation, search, blog, and Zensical TOML config parsing.
- **[Statiq.Framework](https://www.statiq.dev/)** ([repo](https://github.com/statiqdev/Statiq.Framework), MIT): Inspiration for the fluent C# builder model and directory/sidecar metadata cascade (`NuStreamDocs.Metadata`). Only `Statiq.Framework` is referenced.
- **[DocFX](https://dotnet.github.io/docfx/)** ([repo](https://github.com/dotnet/docfx), MIT): Inspiration for .NET assembly documentation and `xrefmap.json` interoperability (`NuStreamDocs.Xrefs`).
- **[Material Design Icons (Pictogrammers)](https://pictogrammers.com/library/mdi/)** ([repo](https://github.com/Templarian/MaterialDesign-SVG), Pictogrammers Free License / Apache 2.0): Inline SVG icon path data baked into `NuStreamDocs.Icons.MaterialDesign`.
- **[Material Web](https://github.com/material-components/material-web)** (Apache 2.0), **[Lit](https://github.com/lit/lit)** (BSD-3-Clause), and **[tslib](https://github.com/Microsoft/tslib)** (0BSD): Vendored browser-runtime subset inside `NuStreamDocs.Theme.Material3`.

Full upstream license notices appear in [LICENSE](../LICENSE) and `src/NuStreamDocs.Theme.Material3/Templates/THIRD_PARTY_NOTICES.md`.
