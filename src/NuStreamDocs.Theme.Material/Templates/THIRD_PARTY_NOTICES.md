# Third-party theme assets

The theme assets that ship inside this assembly as `EmbeddedResource` are derived from:

- [mkdocs-material](https://github.com/squidfunk/mkdocs-material) — © Martin Donath. MIT licensed. The compiled CSS/JS bundle under `Templates/assets/` is lifted verbatim from upstream **mkdocs-material 9.7.6** (`material/templates/assets/`). The theme follows mkdocs-material's own design system — lineage-wise closer to Material Design 2 than MD3; an MD3 port is on the upstream roadmap but not yet landed. The page templates (`page.mustache`, `partials/*.mustache`) are hand-written for our Mustache engine, not lifted from upstream's Jinja2 templates.
- [Zensical](https://github.com/zensical/zensical) — MIT licensed. Behavioral reference for nav, search, blog and privacy plugins.

Attribution must remain when redistributing this assembly.
