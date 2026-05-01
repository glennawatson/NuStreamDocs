# Third-party theme assets

The Material 3 theme assembly ships entirely hand-written assets:

- `Templates/assets/stylesheets/material3.css` — original work,
  inspired structurally by [mkdocs-material](https://github.com/squidfunk/mkdocs-material)
  (© Martin Donath, MIT) but rebuilt against the Material Design 3
  token system.
- `Templates/assets/javascripts/material3.js` — original work.
- `Templates/page.mustache` and the partials under
  `Templates/partials/` — original work.

Material Design 3 is © Google, design system documented at
<https://m3.material.io/>. The token names used in `material3.css`
(`--md-sys-color-*`, `--md-sys-shape-*`, `--md-sys-typescale-*`,
`--md-sys-elevation-*`) follow the public Material Design 3 naming
convention.

The bundled stylesheet references the **Roboto Flex** and
**JetBrains Mono** fonts via Google Fonts at runtime; no font files
are redistributed inside this assembly.
