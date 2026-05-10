// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Building;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// End-to-end pipeline benchmarks comparing the cost of feeding pages into the build via
/// disk-loaded <c>.md</c> files versus through the new synthetic-page surface
/// (<see cref="SyntheticPageSink"/> + <see cref="SyntheticPageSink.RegisterStream(IAsyncEnumerable{SyntheticPage})"/>).
/// Surfaces any per-page overhead the synthetic branch in
/// <c>BuildPipeline.ProcessOnePageAsync</c> adds over the disk fast path.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[SuppressMessage(
    "Major Code Smell",
    "S4462:Calls to \"async\" methods should not be blocking",
    Justification = "BenchmarkDotNet drives benchmarks synchronously; GetResult is the pragmatic way to measure end-to-end async pipelines.")]
public class PipelineSyntheticPagesBenchmarks
{
    /// <summary>Small page count (smoke).</summary>
    private const int SmallPages = 100;

    /// <summary>Medium page count (typical project).</summary>
    private const int MediumPages = 500;

    /// <summary>Large page count (API-generator scale slice; full ~10k corpus is exercised in <see cref="SyntheticPageSinkBenchmarks"/>).</summary>
    private const int LargePages = 2000;

    /// <summary>Markdown payload reused across pages — keeps per-iteration cost focused on the pipeline, not on payload generation.</summary>
    private static readonly byte[] PagePayload = Encoding.UTF8.GetBytes(
        "# Page\n\nbody with **bold** and `code` and a [link](https://example.com).\n\n"
        + "## Section\n\nMore prose.\n");

    /// <summary>Empty input root used by the synthetic-only benchmarks (the pipeline still needs an existing input directory even when every page is synthetic).</summary>
    private string _emptyInputRoot = string.Empty;

    /// <summary>Disk input root pre-populated with <c>Pages</c> markdown files for the disk baseline.</summary>
    private string _diskInputRoot = string.Empty;

    /// <summary>Per-iteration output root.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>Pre-built relative paths used by the synthetic plugin; sized once so the per-iteration cost focuses on pipeline behaviour.</summary>
    private FilePath[] _syntheticPaths = [];

    /// <summary>Gets or sets the page count for the current parameter set.</summary>
    [Params(SmallPages, MediumPages, LargePages)]
    public int Pages { get; set; }

    /// <summary>Allocates the disk corpus and pre-built work items once per parameter set.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _emptyInputRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-synth-in-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        _diskInputRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-disk-in-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        _outputRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-synth-out-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_emptyInputRoot);
        Directory.CreateDirectory(_diskInputRoot);

        _syntheticPaths = new FilePath[Pages];
        for (var i = 0; i < Pages; i++)
        {
            _syntheticPaths[i] = (FilePath)("api/Type" + i.ToString(CultureInfo.InvariantCulture) + ".md");
            File.WriteAllBytes(Path.Combine(_diskInputRoot, "page-" + i.ToString(CultureInfo.InvariantCulture) + ".md"), PagePayload);
        }
    }

    /// <summary>Cleans the temp roots once at the end.</summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        TryDelete(_emptyInputRoot);
        TryDelete(_diskInputRoot);
        TryDelete(_outputRoot);
    }

    /// <summary>Wipes and re-creates the per-iteration output directory.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        TryDelete(_outputRoot);
        Directory.CreateDirectory(_outputRoot);
    }

    /// <summary>Disk baseline: <c>Pages</c> markdown files on disk, no synthetic-page plugin.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark(Baseline = true)]
    public int Disk() =>
        new DocBuilder()
            .WithInput(_diskInputRoot)
            .WithOutput(_outputRoot)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Eager synthetic: a discover plugin pre-registers every page via <see cref="SyntheticPageSink.Add(SyntheticPage)"/>.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int SyntheticEager() =>
        new DocBuilder()
            .WithInput(_emptyInputRoot)
            .WithOutput(_outputRoot)
            .UsePlugin(new EagerSyntheticPlugin(_syntheticPaths, PagePayload))
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Streamed synthetic: a discover plugin registers an <see cref="IAsyncEnumerable{SyntheticPage}"/> the pipeline pulls one at a time.</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int SyntheticStreamed() =>
        new DocBuilder()
            .WithInput(_emptyInputRoot)
            .WithOutput(_outputRoot)
            .UsePlugin(new StreamedSyntheticPlugin(_syntheticPaths, PagePayload))
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Best-effort recursive directory delete.</summary>
    /// <param name="path">Directory path.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>Discover-phase plugin that eagerly seeds <see cref="SyntheticPageSink"/> with every page up front.</summary>
    private sealed class EagerSyntheticPlugin(FilePath[] paths, byte[] payload) : IBuildDiscoverPlugin
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "bench-eager-synth"u8;

        /// <inheritdoc/>
        public PluginPriority DiscoverPriority => new(PluginBand.Earliest);

        /// <inheritdoc/>
        public ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
        {
            for (var i = 0; i < paths.Length; i++)
            {
                context.SyntheticPages.Add(new(paths[i], payload));
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Discover-phase plugin that registers a lazy <see cref="IAsyncEnumerable{SyntheticPage}"/> the pipeline pulls page-by-page.</summary>
    private sealed class StreamedSyntheticPlugin(FilePath[] paths, byte[] payload) : IBuildDiscoverPlugin
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "bench-streamed-synth"u8;

        /// <inheritdoc/>
        public PluginPriority DiscoverPriority => new(PluginBand.Earliest);

        /// <inheritdoc/>
        public ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
        {
            context.SyntheticPages.RegisterStream(StreamPagesAsync(paths, payload));
            return ValueTask.CompletedTask;
        }

        /// <summary>Lazy iterator that yields every <see cref="SyntheticPage"/> for the bench corpus.</summary>
        /// <param name="paths">Pre-built relative paths.</param>
        /// <param name="payload">Shared markdown payload.</param>
        /// <returns>Async stream of pages.</returns>
        private static async IAsyncEnumerable<SyntheticPage> StreamPagesAsync(FilePath[] paths, byte[] payload)
        {
            for (var i = 0; i < paths.Length; i++)
            {
                await Task.Yield();
                yield return new(paths[i], payload);
            }
        }
    }
}
