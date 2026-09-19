# Performance

NuStreamDocs builds documentation sites from Markdown and generated pages. Its
core pipeline handles parsing, rendering, caching, and output, while plugins add
features such as navigation, highlighting, search, and API documentation.

Your build time depends on the content and features you enable. A small Markdown
site, a code-heavy guide, and thousands of generated API pages exercise different
parts of the pipeline.

## Site-build measurements

The 211-page ReactiveUI documentation workload measures parsing, rendering, and
writing a site with different plugin configurations.

| Configuration | Build time | Managed allocation |
|---|---:|---:|
| Core parsing, rendering, and output | 11.20 ms | 770 KB |
| With Markdown extensions | 13.86 ms | 1.96 MB |
| With syntax highlighting | 23.29 ms | 2.14 MB |
| With navigation | 20.62 ms | 2.24 MB |
| With automatic links | 13.68 ms | 956 KB |
| Combined `FullStack` configuration | 87.86 ms | 13.21 MB |

These results correspond to about 19,000 pages per second for the core pipeline
and 2,400 pages per second for the combined configuration. The combined workload
includes Markdown extensions, highlighting, automatic links, navigation,
cross-references, Lunr search, and Mermaid; API extraction adds a separate stage.

For synthetic Markdown files, a 500-page build measured **7.56 ms** for the core
pipeline and **28.85 ms** for the combined configuration. These builds include
disk reads and output writes. Content complexity and plugin selection explain
why per-page costs differ from the real documentation corpus.

Measurements use an AMD Ryzen 7 5800X on Linux, .NET 10.0.7, Release mode, and
BenchmarkDotNet 0.15.8 with three warmups and three measured iterations.
The [full tables](../src/benchmarks/results.md) include individual plugin,
renderer, highlighting, and profiling results.

## What affects your site

- **Page content.** Large code blocks, tables, links, and embedded markup require
  more processing than short prose pages.
- **Plugin selection.** Highlighting, navigation, search indexing, and other
  features add useful work. A measured plugin combination does not represent
  every available plugin or every configuration.
- **Generated API documentation.** NuGet acquisition and API extraction happen
  before normal page rendering. Warm package caches reduce downloads, but the
  selected package and target-framework graphs still determine extraction work.
- **Caching and output.** Fresh-output measurements include rendering and file
  writes. A repeated build can reuse unchanged pages through the build manifest.
  Synthetic pages avoid source-file reads, but their rendered output is still
  written to the site directory.

## Interpreting measurements

Full-site benchmarks measure a specific corpus and plugin setup. Microbenchmarks
measure isolated operations; adding those timings together does not predict a
complete build. Allocation figures count managed memory allocated during an
operation, not the peak RAM needed to run it.

Use the corpus size, runtime, cache state, and enabled plugins alongside each
result. A change in those inputs changes the question the benchmark answers.

## Comparisons with other tools

NuStreamDocs follows a MkDocs-style documentation workflow with an explicit
plugin pipeline. Docfx provides an integrated documentation toolchain with
source/project API extraction, conceptual content, templates, navigation,
cross-references, and site rendering. These are different product scopes.

The published Refit comparison between SourceDocParser and Docfx 2.80.1 measures
API extraction and YAML writing. It does not measure a complete NuStreamDocs
site against a complete Docfx site, and its speed ratio should not be used that
way. See the
[SourceDocParser comparison](https://github.com/glennawatson/SourceDocParserLib/blob/main/docs/performance.md)
for the matched inputs, output differences, and results.

NuStreamDocs' own site-build measurements compare configurations of NuStreamDocs.
They do not establish a speed advantage over MkDocs, Zensical, or Docfx without
an equivalent corpus, feature set, and output comparison.

The [benchmark guide](../src/benchmarks/README.md) contains detailed results and
execution instructions.
