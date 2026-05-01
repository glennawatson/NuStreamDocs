// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Bibliography;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles.Aglc4;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <see cref="BibliographyPlugin"/>.</summary>
/// <remarks>
/// Two scenarios — the all-resolve happy path and the no-marker pass-through path —
/// pin the two costs callers actually care about: the cost of running the bibliography
/// pipeline on a page that uses <c>[@key]</c> markers, and the overhead the plugin
/// imposes on a page that doesn't.
/// </remarks>
[MemoryDiagnoser]
public class BibliographyBenchmarks
{
    /// <summary>Number of <c>[@key]</c> markers stamped into the marker-heavy fixture.</summary>
    private const int Repetitions = 100;

    /// <summary>Pre-built marker-heavy input (every line resolves through a 3-entry database).</summary>
    private byte[] _markerHeavySource = [];

    /// <summary>Pre-built no-marker input (plain prose; exercises the early-out path).</summary>
    private byte[] _noMarkerSource = [];

    /// <summary>Configured plugin instance.</summary>
    private BibliographyPlugin _plugin = null!;

    /// <summary>Allocates fixtures + plugin.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _markerHeavySource = BuildRepeated("Inline [@mabo] then bundled [@gummow; @hca, p 23] follow-up.\n");
        _noMarkerSource = BuildRepeated("Plain prose with no citation markers anywhere on this line.\n");

        var db = new BibliographyDatabaseBuilder()
            .AddCase("mabo", "Mabo v Queensland (No 2)", "(1992) 175 CLR 1", 1992)
            .AddBook("gummow", "Change and Continuity", PersonName.Of("William", "Gummow"), 2018, "Federation Press")
            .AddLegislation("hca", "High Court of Australia Act 1979", "Cth", 1979)
            .Build();
        _plugin = new(new(db, Aglc4Style.Instance, WarnOnMissing: false));
    }

    /// <summary>Marker-heavy fixture — every <c>[@key]</c> resolves and gets rewritten plus a bibliography section appended.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int MarkerHeavyResolve()
    {
        var sink = new ArrayBufferWriter<byte>(_markerHeavySource.Length * 2);
        _plugin.Preprocess(_markerHeavySource, sink);
        return sink.WrittenCount;
    }

    /// <summary>No-marker fixture — exercises the <c>IndexOf("[@")</c> early-out path; should pass through ~unchanged.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int NoMarkerPassThrough()
    {
        var sink = new ArrayBufferWriter<byte>(_noMarkerSource.Length * 2);
        _plugin.Preprocess(_noMarkerSource, sink);
        return sink.WrittenCount;
    }

    /// <summary>Stamps <paramref name="block"/> <see cref="Repetitions"/> times into a UTF-8 buffer.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>Pre-built fixture bytes.</returns>
    private static byte[] BuildRepeated(string block)
    {
        var blockBytes = Encoding.UTF8.GetBytes(block);
        var output = new byte[blockBytes.Length * Repetitions];
        for (var i = 0; i < Repetitions; i++)
        {
            blockBytes.AsSpan().CopyTo(output.AsSpan(i * blockBytes.Length));
        }

        return output;
    }
}
