// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
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
[DebuggerDisplay("BibliographyBenchmarks: markerHeavySource={_markerHeavySource}, noMarkerSource={_noMarkerSource}")]
[ShortRunJob]
[MemoryDiagnoser]
public class BibliographyBenchmarks
{
    /// <summary>Number of <c>[@key]</c> markers stamped into the marker-heavy fixture.</summary>
    private const int Repetitions = 100;

    /// <summary>Headroom factor for the output writer (footnotes + bibliography section roughly double the body length on the marker-heavy fixture; 4× covers the spread).</summary>
    private const int OutputExpansionFactor = 4;

    /// <summary>Decision year of the case fixture.</summary>
    private const int CaseYear = 1992;

    /// <summary>Publication year of the book fixture.</summary>
    private const int BookYear = 2018;

    /// <summary>Enactment year of the legislation fixture.</summary>
    private const int LegislationYear = 1979;

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
        _markerHeavySource = BuildRepeated("Inline [@mabo] then bundled [@gummow; @hca, p 23] follow-up.\n"u8);
        _noMarkerSource = BuildRepeated("Plain prose with no citation markers anywhere on this line.\n"u8);

        var db = new BibliographyDatabaseBuilder()
            .AddCase([.. "mabo"u8], [.. "Mabo v Queensland (No 2)"u8], [.. "(1992) 175 CLR 1"u8], CaseYear)
            .AddBook(
            [.. "gummow"u8],
            [.. "Change and Continuity"u8],
            PersonName.Of("William", "Gummow"),
            BookYear,
            [.. "Federation Press"u8]).AddLegislation([.. "hca"u8], [.. "High Court of Australia Act 1979"u8], [.. "Cth"u8], LegislationYear)
            .Build();
        _plugin = new(new(db, Aglc4Style.Instance, false));
    }

    /// <summary>Marker-heavy fixture, renting from <see cref="PageBuilderPool"/> to mirror production.</summary>
    /// <returns>Bytes written.</returns>
    /// <remarks>Every <c>[@key]</c> resolves and gets rewritten plus a bibliography section appended.</remarks>
    [Benchmark]
    public int MarkerHeavyResolve()
    {
        using var rental = PageBuilderPool.Rent(_markerHeavySource.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new("page.md", _markerHeavySource, rental.Writer);
        _plugin.PreRender(in ctx);
        return rental.Writer.WrittenCount;
    }

    /// <summary>No-marker fixture — exercises the <c>IndexOf("[@")</c> early-out path; should pass through ~unchanged.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int NoMarkerPassThrough()
    {
        using var rental = PageBuilderPool.Rent(_noMarkerSource.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new("page.md", _noMarkerSource, rental.Writer);
        _plugin.PreRender(in ctx);
        return rental.Writer.WrittenCount;
    }

    /// <summary>Stamps <paramref name="block"/> <see cref="Repetitions"/> times into a UTF-8 buffer.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>Pre-built fixture bytes.</returns>
    private static byte[] BuildRepeated(ReadOnlySpan<byte> block)
    {
        var output = new byte[block.Length * Repetitions];
        for (var i = 0; i < Repetitions; i++)
        {
            block.CopyTo(output.AsSpan(i * block.Length));
        }

        return output;
    }
}
