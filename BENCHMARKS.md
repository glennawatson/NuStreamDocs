# Benchmarks

Numbers from the in-repo BenchmarkDotNet harness under `src/benchmarks/NuStreamDocs.Benchmarks/`. Reproduce locally with:

```bash
cd src
dotnet run --project benchmarks/NuStreamDocs.Benchmarks --configuration Release -- --filter "*"
```

**Hardware / runtime for every table below:** AMD Ryzen 7 5800X (8 physical / 16 logical cores), Linux, .NET 10.0.7, BenchmarkDotNet v0.15.8 `ShortRunJob` (3 warmup + 3 measurement iterations, single launch).

Allocation columns are from `[MemoryDiagnoser]`; the cross-suite alloc and CPU profiles are from a separate `EventPipeProfiler(GcVerbose)` / `EventPipeProfiler(CpuSampling)` pass over every benchmark variant.

---

## End-to-end build, real corpus (211 markdown files, 7.6 MB)

The reference fixture is a snapshot of the [ReactiveUI](https://github.com/reactiveui/website) website docs. Each row layers one plugin onto the same `DocBuilder` and runs the build to a temp directory.

| Method | Mean | Allocated | Per page |
|---|--:|--:|--:|
| Baseline (parse → render → emit) | **11.20 ms** | 770 KB | 53 µs |
| WithMarkdownExtensions | 13.86 ms | 1.96 MB | 66 µs |
| WithHighlight | 23.29 ms | 2.14 MB | 110 µs |
| WithNav | 20.62 ms | 2.24 MB | 98 µs |
| WithMagicLink | 13.68 ms | 956 KB | 65 µs |
| WithSnippets | 11.85 ms | 768 KB | 56 µs |
| WithMacros | 11.81 ms | 950 KB | 56 µs |
| WithBibliography | 11.31 ms | 901 KB | 54 µs |
| WithMdiIcons (~7,400 SVG entries) | 12.30 ms | 1.15 MB | 58 µs |
| WithSphinxInventory | 11.94 ms | 2.45 MB | 57 µs |
| **FullStack** (every shipped plugin) | **87.86 ms** | 13.21 MB | 417 µs |
| EverythingStack (kitchen sink) | 65.58 ms | 11.70 MB | 311 µs |

**Throughput**: ~19,000 pages/sec for the parse → render → emit core. ~2,400 pages/sec with the entire plugin stack on.

---

## Synthetic build pipeline (no I/O)

In-process build over synthetic markdown — same plugin stack, no disk reads. Shows JIT-tiering benefit between 50 and 500 pages.

| Method | Pages | Mean | Per page | Allocated |
|---|--:|--:|--:|--:|
| Baseline | 50 | 1.115 ms | 22 µs | 184 KB |
| WithMarkdownExtensions | 50 | 1.805 ms | 36 µs | 222 KB |
| WithHighlight | 50 | 1.209 ms | 24 µs | 223 KB |
| WithNav | 50 | 2.692 ms | 54 µs | 325 KB |
| WithMermaid | 50 | 1.067 ms | 21 µs | 185 KB |
| **FullStackInProcess** | **50** | **3.933 ms** | **79 µs** | **436 KB** |
| Baseline | 500 | 7.561 ms | 15 µs | 1.69 MB |
| WithMarkdownExtensions | 500 | 13.160 ms | 26 µs | 1.91 MB |
| WithHighlight | 500 | 8.949 ms | 18 µs | 1.79 MB |
| WithNav | 500 | 20.564 ms | 41 µs | 2.78 MB |
| WithMermaid | 500 | 7.585 ms | 15 µs | 1.69 MB |
| **FullStackInProcess** | **500** | **28.849 ms** | **58 µs** | **3.34 MB** |

---

## Render core — zero allocations

Pure parse + emit hot path with no plugins. The renderer streams to a pooled `IBufferWriter<byte>` and never allocates per page.

| Stage | Input | Mean | Allocated |
|---|---|--:|--:|
| `BlockScanner.Scan` | 100 paragraphs | 15.68 µs | **0 B** |
| `BlockScanner.Scan` | 1000 paragraphs | 159.65 µs | **0 B** |
| `BlockScanner.Scan` | 100 comment-heavy | 6.91 µs | **0 B** |
| `BlockScanner.Scan` | 1000 comment-heavy | 67.28 µs | **0 B** |
| `Renderer.Render` | 100 paragraphs | 14.09 µs | **0 B** |
| `Renderer.Render` | 1000 paragraphs | 138.31 µs | **0 B** |
| `HtmlEscape.EscapeText` | clean payload | 2.55 µs | **0 B** |
| `HtmlEscape.EscapeText` | heavy payload | 162.86 µs | **0 B** |
| `HtmlEscape.EscapeAttribute` | clean payload | 2.84 µs | **0 B** |
| `HtmlEscape.EscapeAttribute` | heavy payload | 63.87 µs | **0 B** |

---

## Syntax highlighter (per snippet, zero allocations)

Inline highlighter writing tokens directly into the output `IBufferWriter<byte>`.

| Language | Mean | Allocated |
|---|--:|--:|
| Diff | 12.69 µs | 0 B |
| Xml | 35.05 µs | 0 B |
| Json | 41.27 µs | 0 B |
| Bash | 42.76 µs | 0 B |
| Html | 45.70 µs | 0 B |
| Yaml | 52.04 µs | 0 B |
| PowerShell | 56.64 µs | 0 B |
| Python | 64.71 µs | 0 B |
| C# | 75.81 µs | 0 B |
| TypeScript | 79.85 µs | 0 B |
| Razor | 91.98 µs | 0 B |
| JavaScript | 130.70 µs | 0 B |
| F# | 132.02 µs | 0 B |

---

## Selected per-plugin micro-benchmarks

### Bibliography (citation marker scan)

| Scenario | Mean | Allocated |
|---|--:|--:|
| `MarkerHeavyResolve` | 34.66 µs | 0 B |
| `NoMarkerPassThrough` | 225 ns | 0 B |

### Magic-link (autolinker)

| Scenario | Mean | Allocated |
|---|--:|--:|
| `UrlsOnly` | 15.85 µs | 32 KB |
| `IssueRefsExpanded` | 22.14 µs | 23 KB |
| `MentionsExpanded` | 16.72 µs | 27 KB |
| `CombinedRxuiShape` | 25.86 µs | 34 KB |

### Search index scan

| Scenario | Mean | Allocated |
|---|--:|--:|
| `PagefindScanShort` | 565 ns | 208 B |
| `LunrScanShort` | 565 ns | 208 B |
| `PagefindScanWithFrontmatter` | 710 ns | 248 B |
| `PagefindScanLong` | 101.25 µs | 33 KB |

### Nav rendering (active-trail HTML)

| Scenario | Mean | Allocated |
|---|--:|--:|
| `RenderFull` | 2.35 µs | 0 B |
| `RenderPruned` | 2.34 µs | 0 B |

---

## Cross-suite allocation profile (`GcVerbose`, 266 traces aggregated)

Where the heap goes when the entire benchmark suite runs end-to-end.

| Share | Type |
|--:|---|
| 81.4% | `System.Byte[]` (the UTF-8 buffers the pipeline is built around) |
| 4.9% | `System.Buffers.ArrayBufferWriter<byte>` (page-output sinks) |
| 4.7% | `SyntheticPage[]` (benchmark fixture inputs) |
| 3.6% | `System.String` (boundary materialization for diagnostics + APIs) |
| 2.6% | `NuStreamDocs.Common.ByteRange[]` (link-extractor result spans) |
| < 0.5% each | every other type |

UTF-8 byte buffers dominate by design: input is read as bytes, parsed with `ReadOnlySpan<byte>`, and emitted to byte sinks; strings only materialize at API boundaries.

---

## Cross-suite CPU profile (`CpuSampling`, 1.96M samples aggregated)

Self-time across the entire benchmark suite. The hot path is BCL-bound — the JIT lowers our scanners onto SIMD-vectorized BCL helpers.

| Share | Function |
|--:|---|
| 10.9% | `System.SpanHelpers.IndexOf` (vectorized scanner) |
| 10.0% | `System.Buffer.MemmoveInternal` (buffer copies) |
| 6.6% | GC poll points |
| 1.8% | `System.SpanHelpers.NonPackedIndexOfValueType` |
| 1.7% | `System.SpanHelpers.Memmove` |
| **1.1%** | `LinkExtractor.ExtractHeadingIdRanges` (top NuStreamDocs frame) |
| 0.84% | `Toc.HeadingSlugifier.SlugifyToBytes` |
| 0.64% | `Search.SearchPluginBase.Scan` |
| 0.61% | `Toc.HeadingSlugifier.AssignSlugs` |
| 0.48% | `Theme.Common.IconShortcodeRewriter.Rewrite` |
| < 0.5% each | every other NuStreamDocs method |

No single internal NuStreamDocs method exceeds 1.1% of total CPU — work is spread across the pipeline rather than bottlenecked in one place.

---

## Reproducing

```bash
cd src

# Full sweep (every benchmark variant). ~25-30 min on developer hardware.
dotnet run --project benchmarks/NuStreamDocs.Benchmarks --configuration Release -- --filter "*"

# One class
dotnet run --project benchmarks/NuStreamDocs.Benchmarks --configuration Release -- --filter "*RxuiCorpus*"

# With allocation profiling — emits a .nettrace next to each result
#   (the GcVerbose attribute is already on RxuiCorpusBenchmarks +
#   ProfiledPhaseBenchmarks; see src/tools/NuStreamDocs.AllocReport
#   to turn the trace into a markdown table).
dotnet run --project benchmarks/NuStreamDocs.Benchmarks --configuration Release -- --filter "*RxuiCorpus*"
```

CSV / Markdown reports land under `src/BenchmarkDotNet.Artifacts/results/`. The full BDN session log + per-method details are in `*-report-github.md` per benchmark class.
