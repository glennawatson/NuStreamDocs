// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Macros;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <see cref="MacrosPlugin"/>.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class MacrosBenchmarks
{
    /// <summary>Number of <c>{{ name }}</c> markers stamped into each fixture.</summary>
    private const int Repetitions = 100;

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
        _markerHeavySource = BuildRepeated("Project: {{ project }} Version: {{ version }} Author: {{ author }}\n");
        _noMarkerSource = BuildRepeated("Plain prose line with no curly markers anywhere here at all.\n");

        var vars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["project"] = "ReactiveUI",
            ["version"] = "20.0.0",
            ["author"] = "Glenn Watson",
            ["year"] = "2026",
        };
        _plugin = new(new(vars, EscapeHtml: false, WarnOnMissing: false));
    }

    /// <summary>Marker-heavy fixture — every <c>{{ name }}</c> resolves through the variable map.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int MarkerHeavyResolve()
    {
        var sink = new ArrayBufferWriter<byte>(_markerHeavySource.Length * 2);
        _plugin.Preprocess(_markerHeavySource, sink);
        return sink.WrittenCount;
    }

    /// <summary>No-marker fixture — exercises the early-out path.</summary>
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
