// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Macros;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <see cref="MacrosPlugin"/>.</summary>
[DebuggerDisplay("MacrosBenchmarks: markerHeavySource={_markerHeavySource}, noMarkerSource={_noMarkerSource}")]
[ShortRunJob]
[MemoryDiagnoser]
public class MacrosBenchmarks
{
    /// <summary>Number of <c>{{ name }}</c> markers stamped into each fixture.</summary>
    private const int Repetitions = 100;

    /// <summary>Reserves room for substitutions larger than their markers.</summary>
    private const int OutputExpansionFactor = 2;

    /// <summary>Pre-built marker-heavy fixture — alternating known + unknown variable names.</summary>
    private byte[] _markerHeavySource = [];

    /// <summary>Pre-built no-marker fixture — exercises the early-out path.</summary>
    private byte[] _noMarkerSource = [];

    /// <summary>Configured plugin instance.</summary>
    private MacrosPlugin _plugin = null!;

    /// <summary>Allocates fixtures + plugin.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _markerHeavySource = BuildRepeated("Project: {{ project }} Version: {{ version }} Author: {{ author }}\n"u8);
        _noMarkerSource = BuildRepeated("Plain prose line with no curly markers anywhere here at all.\n"u8);

        Dictionary<byte[], byte[]> vars = [with(ByteArrayComparer.Instance)];
        vars[[.. "project"u8]] = [.. "ReactiveUI"u8];
        vars[[.. "version"u8]] = [.. "20.0.0"u8];
        vars[[.. "author"u8]] = [.. "Glenn Watson"u8];
        vars[[.. "year"u8]] = [.. "2026"u8];
        _plugin = new(new(vars, false, false));
    }

    /// <summary>Marker-heavy fixture — every <c>{{ name }}</c> resolves through the variable map.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int MarkerHeavyResolve()
    {
        ArrayBufferWriter<byte> sink = new(_markerHeavySource.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new("page.md", _markerHeavySource, sink);
        _plugin.PreRender(in ctx);
        return sink.WrittenCount;
    }

    /// <summary>No-marker fixture — exercises the early-out path.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int NoMarkerPassThrough()
    {
        ArrayBufferWriter<byte> sink = new(_noMarkerSource.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new("page.md", _noMarkerSource, sink);
        _plugin.PreRender(in ctx);
        return sink.WrittenCount;
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
