// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using NuStreamDocs.Autorefs;
using NuStreamDocs.Building;
using NuStreamDocs.Highlight;
using NuStreamDocs.MarkdownExtensions;
using NuStreamDocs.Mermaid;
using NuStreamDocs.Nav;
using NuStreamDocs.Search;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Scale-test benchmark that points DocBuilder at the local rxui website
/// corpus (~13.8K markdown files / ~72 MB) and runs an end-to-end build
/// with most in-process plugins enabled. Pinned via an absolute path on
/// the maintainer's workstation; gated on the directory existing so CI
/// and other contributors can run the rest of the suite without it.
/// </summary>
/// <remarks>
/// Profiled with <c>EventPipeProfilerAttribute</c> at
/// <c>EventPipeProfile.GcVerbose</c> so the resulting
/// <c>.nettrace</c> can be fed through <c>smkd-allocreport</c> to
/// surface real hotspots — the synthetic corpora elsewhere don't
/// stress real-world heading/link/code-block density the way the
/// rxui docs do.
/// <para>
/// `[ShortRunJob]` because each iteration is multi-second; one launch
/// + warmup + a few iterations is plenty for relative ranking.
/// </para>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
[SuppressMessage(
    "Major Code Smell",
    "S4462:Calls to \"async\" methods should not be blocking",
    Justification = "BenchmarkDotNet drives benchmarks synchronously; GetResult is the pragmatic way to measure end-to-end async pipelines.")]
public class RxuiCorpusBenchmarks
{
    /// <summary>Absolute path to the maintainer's local rxui-website corpus checkout.</summary>
    private const string RxuiDocsRoot = "/home/glennw/source/rxui/website/docs";

    /// <summary>Per-iteration output directory created under the system temp.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>Skips the benchmark on machines that don't have the corpus checked out.</summary>
    /// <returns>True when the corpus directory is missing.</returns>
    public static bool ShouldSkip() => !Directory.Exists(RxuiDocsRoot);

    /// <summary>Allocates a fresh per-iteration output directory.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        TryDelete(_outputRoot);
        _outputRoot = Path.Combine(
            Path.GetTempPath(),
            "smkd-rxui-out-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_outputRoot);
    }

    /// <summary>Cleans the last iteration's output directory.</summary>
    [GlobalCleanup]
    public void GlobalCleanup() => TryDelete(_outputRoot);

    /// <summary>Baseline: pure render + write, no plugins.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark(Baseline = true)]
    public int Baseline() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with the markdown-extension bundle (admonitions, details, tabs, footnotes, …).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithMarkdownExtensions() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with syntax highlighting on every fenced block.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithHighlight() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseHighlight()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with nav generation (full discovery + per-page render of the active branch).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int WithNav() =>
        new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseNav()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Build with the full in-process plugin stack — markdown extensions + highlight + nav + autorefs + search + mermaid.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int FullStack()
    {
        var registry = new AutorefsRegistry();
        return new DocBuilder()
            .WithInput(RxuiDocsRoot)
            .WithOutput(_outputRoot)
            .UseCommonMarkdownExtensions()
            .UseHighlight()
            .UseNav()
            .UseAutorefs(registry)
            .UseSearch()
            .UseMermaid()
            .BuildAsync()
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Best-effort recursive directory delete.</summary>
    /// <param name="path">Directory path.</param>
    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; benchmarks fail-soft on tear-down.
        }
    }
}
