// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Arithmatex.MathJax;

namespace NuStreamDocs.Benchmarks;

/// <summary>Constructor + per-page <c>WriteHeadExtra</c> cost for <see cref="MathJaxPlugin"/>.</summary>
/// <remarks>
/// Construction composes the head fragment once via <c>string.Format</c>; <c>WriteHeadExtra</c>
/// is a single sink write of the cached bytes. The constructor benchmark catches any future
/// regression in the formatting path; the per-page benchmark captures the steady-state cost
/// (which should be a single byte-copy + null check).
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class MathJaxPluginBenchmarks
{
    /// <summary>Initial sink capacity — head fragment is ~400 bytes, so 512 sidesteps a resize.</summary>
    private const int SinkCapacity = 512;

    /// <summary>Reused plugin for the per-page benchmark.</summary>
    private MathJaxPlugin _plugin = null!;

    /// <summary>Reused sink so per-iteration allocation reflects the head writer, not the buffer.</summary>
    private ArrayBufferWriter<byte> _sink = null!;

    /// <summary>Builds a single plugin + reused sink.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plugin = new();
        _sink = new(SinkCapacity);
    }

    /// <summary>Plugin construction — exercises the head-fragment composition path.</summary>
    /// <returns>The new plugin (kept so the JIT can't elide).</returns>
    [Benchmark]
    public MathJaxPlugin Construct() => new();

    /// <summary>Per-page head emission — the steady-state hot path.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int WriteHead()
    {
        _sink.ResetWrittenCount();
        _plugin.WriteHeadExtra(_sink);
        return _sink.WrittenCount;
    }
}
