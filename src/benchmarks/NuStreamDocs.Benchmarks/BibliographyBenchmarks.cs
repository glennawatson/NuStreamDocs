// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Bibliography;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles.Aglc4;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <see cref="BibliographyPlugin"/>.</summary>
/// <remarks>
/// Two scenarios — the all-resolve happy path and the no-marker pass-through path —
/// pin the two costs callers actually care about: the cost of running the bibliography
/// pipeline on a page that uses <c>[@key]</c> markers, and the overhead the plugin
/// imposes on a page that doesn't.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class BibliographyBenchmarks
{
    /// <summary>Number of <c>[@key]</c> markers stamped into the marker-heavy fixture.</summary>
    private const int Repetitions = 100;

    /// <summary>Headroom factor for the output writer (footnotes + bibliography section roughly double the body length on the marker-heavy fixture; 4× covers the spread).</summary>
    private const int OutputExpansionFactor = 4;

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

    /// <summary>Marker-heavy fixture, renting from <see cref="PageBuilderPool"/> to mirror production.</summary>
    /// <remarks>Every <c>[@key]</c> resolves and gets rewritten plus a bibliography section appended.</remarks>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int MarkerHeavyResolve()
    {
        using var rental = PageBuilderPool.Rent(_markerHeavySource.Length * OutputExpansionFactor);
        var ctx = new PagePreRenderContext("page.md", _markerHeavySource, rental.Writer);
        _plugin.PreRender(in ctx);
        return rental.Writer.WrittenCount;
    }

    /// <summary>No-marker fixture — exercises the <c>IndexOf("[@")</c> early-out path; should pass through ~unchanged.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int NoMarkerPassThrough()
    {
        using var rental = PageBuilderPool.Rent(_noMarkerSource.Length * OutputExpansionFactor);
        var ctx = new PagePreRenderContext("page.md", _noMarkerSource, rental.Writer);
        _plugin.PreRender(in ctx);
        return rental.Writer.WrittenCount;
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
