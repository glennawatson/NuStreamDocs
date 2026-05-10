// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Baseline benchmark for the C# API generator's synthetic-page bridge — the
/// <c>CallbackPageSink</c> + <see cref="Channel{T}"/> + <see cref="SyntheticPageSink.RegisterStream"/>
/// path. Doesn't run Roslyn; instead simulates an emitter that produces
/// <see cref="Pages"/> in-memory pages so the bench measures the bridge
/// overhead specifically. Useful as a reference for any future change to
/// either the channel shape or the streaming-sink contract.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[SuppressMessage(
    "Major Code Smell",
    "S4462:Calls to \"async\" methods should not be blocking",
    Justification = "BenchmarkDotNet drives benchmarks synchronously; GetResult is the pragmatic way to measure end-to-end async pipelines.")]
public class CSharpApiGeneratorBridgeBenchmarks
{
    /// <summary>Small page count (smoke).</summary>
    private const int SmallPages = 100;

    /// <summary>Medium page count (typical mid-size assembly).</summary>
    private const int MediumPages = 1000;

    /// <summary>Large page count (Roslyn-sized assembly slice).</summary>
    private const int LargePages = 5000;

    /// <summary>Reused page payload — keeps the per-iteration cost focused on the bridge, not on payload generation.</summary>
    private static readonly byte[] PagePayload = "# Type X\n\nbody"u8.ToArray();

    /// <summary>Pre-built relative paths the simulated emitter pretends to emit; sized once so the per-iteration cost focuses on bridge behaviour.</summary>
    private string[] _emitterRelativePaths = [];

    /// <summary>Gets or sets the page count for the current parameter set.</summary>
    [Params(SmallPages, MediumPages, LargePages)]
    public int Pages { get; set; }

    /// <summary>Allocates the emitter's per-page relative paths once per parameter set.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _emitterRelativePaths = new string[Pages];
        for (var i = 0; i < Pages; i++)
        {
            _emitterRelativePaths[i] = "Splat/Splat/Type" + i.ToString(CultureInfo.InvariantCulture) + ".md";
        }
    }

    /// <summary>
    /// Runs the bridge end-to-end: emitter pushes pages into the channel via a sink
    /// callback; the registered async stream drains them one at a time.
    /// </summary>
    /// <returns>Number of synthetic pages drained (returned so BenchmarkDotNet doesn't elide the call).</returns>
    [Benchmark]
    public int Bridge()
    {
        var channel = Channel.CreateUnbounded<SyntheticPage>(new()
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

        // Producer — mirrors what CSharpApiGeneratorPlugin's CallbackPageSink does:
        // for each emitter page, build a SyntheticPage and TryWrite into the channel.
        var producer = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < _emitterRelativePaths.Length; i++)
                {
                    var virtualPath = (FilePath)("api/" + _emitterRelativePaths[i]);
                    channel.Writer.TryWrite(new(virtualPath, PagePayload));
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        });

        SyntheticPageSink sink = new();
        sink.RegisterStream(StreamFromChannelAsync(channel.Reader, producer, CancellationToken.None));
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

    /// <summary>Drains <paramref name="reader"/>, then surfaces any background-task exception once the channel completes — matches the production plugin's pattern.</summary>
    /// <param name="reader">Channel reader the producer writes pages into.</param>
    /// <param name="producer">Background producer task; awaited after the channel closes.</param>
    /// <param name="cancellationToken">Cancellation token observed between pages.</param>
    /// <returns>The async stream of synthetic pages handed to the pipeline.</returns>
    private static async IAsyncEnumerable<SyntheticPage> StreamFromChannelAsync(
        ChannelReader<SyntheticPage> reader,
        Task producer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var page in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return page;
        }

        await producer.ConfigureAwait(false);
    }
}
