# NuStreamDocs — Punch List

_Last audit: 2026-05-01_

The pitch and feature list live in the README. This is the punch list of
**what's still pending**. Items get deleted from this file once they
ship; the file is meant to stay short.

## mkdocs / Material gaps

- **Social cards.** Deferred until SkiaSharp 4 lands (avoids the v3
  AOT/ABI churn).

## DocFX gaps

- **REST API generator.** OpenAPI / Swagger spec → reference pages.
  DocFX ships this; we don't. Most-requested DocFX feature still
  missing.
- **PDF output.** DocFX has a PDF builder; would need an HTML→PDF
  bridge (PuppeteerSharp or QuestPDF) and per-page chrome trimming.
- **API tree live filter.** TOC search box that narrows the rendered
  reference tree without a page reload — purely a theme-side feature.

## Statiq gaps

- **Image pipeline.** `Optimise` does compression; resize +
  responsive-srcset emission is not yet wired. Statiq users expect
  this as a first-class step.

## Internal polish (not feature parity)

- **Watch-mode incremental nav rebuild.** `WatchLoop` re-runs the full
  nav build on any change. Diff-based patch would help on the rxui
  corpus.
- **End-to-end Material golden-fixture test.** Plugin-level integration
  tests are green; we don't yet have an HTML golden comparable to
  `ZensicalRenderSmokeTests` on the parser side. Only existing snapshot
  test is `HtmlSnapshotRewriterTests` in the Optimise project.
- **Public-API surface review for 1.0.** Audit pass needed before
  tagging — concrete-collection rules, params-array boundaries, plugin
  contract shape.

## Active task-list items

- **#77 — combined slice.** Content flags + redirect-maps + search
  tuning. In-progress slice spanning Sitemap, Search, and the
  redirects emitter.
