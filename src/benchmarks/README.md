# Benchmarks

The [performance overview](../../docs/performance.md) explains the results for
documentation authors. This guide covers the measured workloads and how to run
them. See the [full result tables](results.md) for pipeline, renderer, plugin,
and profiling measurements.

Run the harness from `src/` using the repository's SDK and package configuration:

```bash
dotnet run --project benchmarks/NuStreamDocs.Benchmarks \
  --framework net10.0 --configuration Release -- \
  --filter '*RxuiCorpusBenchmarks*' '*BuildPipelineBenchmarks*' \
  --warmupCount 5 --iterationCount 15
```

`RxuiCorpusBenchmarks` reads the ReactiveUI website checkout configured by the
harness. Record its Markdown file count, total bytes, and content fingerprint
with the results. `BuildPipelineBenchmarks` creates synthetic Markdown files
and includes output writes; its measurements are not an I/O-free parser test.

Use `RxuiCorpusAllocationBenchmarks` for a separate allocation-profile pass.
Profiled timings must not be compared with unprofiled timings.
