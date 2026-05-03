# Third-party theme assets

The Material 3 theme assembly ships hand-written theme assets plus a vendored
subset of the official Material Web runtime:

- `Templates/assets/stylesheets/material3.css` — original work,
  inspired structurally by [mkdocs-material](https://github.com/squidfunk/mkdocs-material)
  (© Martin Donath, MIT) but rebuilt against the Material Design 3
  token system.
- `Templates/assets/javascripts/material3.js` — original work.
- `Templates/assets/javascripts/material-web-init.js` — original work glue
  that imports the vendored official Material Web modules used by the docs shell.
- `Templates/page.mustache` and the partials under
  `Templates/partials/` — original work.
- `Templates/assets/vendor/@material/web/**` — vendored subset of
  [@material/web 2.4.1](https://github.com/material-components/material-web),
  © Google LLC, Apache-2.0.
- `Templates/assets/vendor/lit/**`,
  `Templates/assets/vendor/lit-html/**`,
  `Templates/assets/vendor/lit-element/**`,
  `Templates/assets/vendor/@lit/reactive-element/**`,
  `Templates/assets/vendor/@lit-labs/ssr-dom-shim/**` — vendored subset of
  the [Lit project](https://github.com/lit/lit), © Google LLC, BSD-3-Clause.
- `Templates/assets/vendor/tslib/**` — vendored subset of
  [tslib 2.8.1](https://github.com/Microsoft/tslib), © Microsoft,
  0BSD.

Material Design 3 is © Google, design system documented at
<https://m3.material.io/>. The token names used in `material3.css`
(`--md-sys-color-*`, `--md-sys-shape-*`, `--md-sys-typescale-*`,
`--md-sys-elevation-*`) follow the public Material Design 3 naming
convention.

The bundled stylesheet references the **Source Sans 3** and
**JetBrains Mono** fonts via Google Fonts at runtime; no font files
are redistributed inside this assembly.
