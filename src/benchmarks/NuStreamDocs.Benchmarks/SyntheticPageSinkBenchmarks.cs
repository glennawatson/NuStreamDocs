// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Microbenchmarks for <see cref="SyntheticPageSink"/> covering both registration paths
/// (eager <see cref="SyntheticPageSink.Add(SyntheticPage)"/> for small producers and
/// <see cref="SyntheticPageSink.RegisterStream(IAsyncEnumerable{SyntheticPage})"/> for
/// fan-out producers like the C# API generator) plus the
/// <see cref="SyntheticPageSink.DrainAsync(CancellationToken)"/> iteration cost the
/// build pipeline pays per build.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[SuppressMessage(
    "Major Code Smell",
    "S4462:Calls to \"async\" methods should not be blocking",
    Justification = "BenchmarkDotNet drives benchmarks synchronously; GetResult is the pragmatic way to measure end-to-end async pipelines.")]
public class SyntheticPageSinkBenchmarks
{
    /// <summary>Small fan-out (one-page-per-tag style synthesis).</summary>
    private const int SmallFanout = 100;

    /// <summary>Medium fan-out (typical mid-size project nav / tag set).</summary>
    private const int MediumFanout = 1000;

    /// <summary>Large fan-out (C# API generator scale — ~10k pages on a Roslyn-sized corpus).</summary>
    private const int LargeFanout = 10000;

    /// <summary>Reused page payload — keeps the per-iteration cost focused on the sink, not on byte-array allocation.</summary>
    private static readonly byte[] PagePayload = "# Page\n\nbody"u8.ToArray();

    /// <summary>Pre-built work items for the eager <c>Add</c> benchmarks; sized to the worst-case <see cref="Pages"/> param.</summary>
    private SyntheticPage[] _items = [];

    /// <summary>Gets or sets the page count for the current parameter set.</summary>
    [Params(SmallFanout, MediumFanout, LargeFanout)]
    public int Pages { get; set; }

    /// <summary>Allocates the per-page work items once so the per-iteration cost focuses on sink behaviour rather than allocation.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _items = new SyntheticPage[Pages];
        for (var i = 0; i < Pages; i++)
        {
            _items[i] = new((FilePath)("api/Type" + i + ".md"), PagePayload);
        }
    }

    /// <summary>Eagerly adds <c>Pages</c> entries via <see cref="SyntheticPageSink.Add(SyntheticPage)"/>.</summary>
    /// <returns>Final count.</returns>
    [Benchmark(Baseline = true)]
    public int EagerAdd()
    {
        SyntheticPageSink sink = new();
        for (var i = 0; i < _items.Length; i++)
        {
            sink.Add(_items[i]);
        }

        return sink.Count;
    }

    /// <summary>Eagerly adds entries then drains them via <see cref="SyntheticPageSink.DrainAsync(CancellationToken)"/> — what the build pipeline pays per build.</summary>
    /// <returns>Pages drained.</returns>
    [Benchmark]
    public int EagerAddThenDrain()
    {
        SyntheticPageSink sink = new();
        for (var i = 0; i < _items.Length; i++)
        {
            sink.Add(_items[i]);
        }

        return DrainSync(sink);
    }

    /// <summary>Registers the items as one async stream; mimics the API-generator path where pages flow through a Channel.</summary>
    /// <returns>Pages drained.</returns>
    [Benchmark]
    public int RegisterStreamThenDrain()
    {
        SyntheticPageSink sink = new();
        sink.RegisterStream(IteratePages(_items));
        return DrainSync(sink);
    }

    /// <summary>Drains the sink synchronously so BDN can time the full iteration without async overhead in the timer loop.</summary>
    /// <param name="sink">Sink to drain.</param>
    /// <returns>Page count yielded.</returns>
    private static int DrainSync(SyntheticPageSink sink) =>
        CountDrainAsync(sink).GetAwaiter().GetResult();

    /// <summary>Async helper that walks the full <c>DrainAsync</c> stream and returns the count.</summary>
    /// <param name="sink">Sink to drain.</param>
    /// <returns>Page count yielded.</returns>
    private static async Task<int> CountDrainAsync(SyntheticPageSink sink)
    {
        var count = 0;
        await foreach (var page in sink.DrainAsync(CancellationToken.None).ConfigureAwait(false))
        {
            _ = page;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Pure-iterator helper that yields every page in <paramref name="items"/> as an async stream.
    /// Uses an already-completed <see cref="Task"/> await so the iteration-state-machine cost is
    /// measured without the thread-pool round-trip <see cref="Task.Yield"/> would force — that
    /// matches the production path where the C# API generator's <c>ChannelReader.ReadAllAsync</c>
    /// completes synchronously when an item is already enqueued.
    /// </summary>
    /// <param name="items">Work items to yield.</param>
    /// <returns>Async stream of pages.</returns>
    private static async IAsyncEnumerable<SyntheticPage> IteratePages(SyntheticPage[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return items[i];
        }
    }
}
