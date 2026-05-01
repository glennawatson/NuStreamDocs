// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Html;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <c>HtmlEscape.EscapeText</c>.</summary>
/// <remarks>
/// Two parameter sets: a clean payload with no escapable bytes (best
/// case — vector copy) and a punctuation-heavy payload that exercises
/// every escape branch.
/// </remarks>
[MemoryDiagnoser]
public class HtmlEscapeBenchmarks
{
    /// <summary>Repetitions to grow the input enough to time.</summary>
    private const int Repetitions = 2000;

    /// <summary>Pre-built clean buffer (no escapable bytes).</summary>
    private byte[] _clean = [];

    /// <summary>Pre-built buffer where every line contains escapable bytes.</summary>
    private byte[] _heavy = [];

    /// <summary>Generates the input buffers once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var clean = new StringBuilder();
        var heavy = new StringBuilder();
        for (var i = 0; i < Repetitions; i++)
        {
            clean.Append("plain ascii line without any escape candidates here.\n");
            heavy.Append("Tom & Jerry <said> \"hi\" 'there' & 'again' >> end\n");
        }

        _clean = Encoding.UTF8.GetBytes(clean.ToString());
        _heavy = Encoding.UTF8.GetBytes(heavy.ToString());
    }

    /// <summary>Escape pass over the no-escapes payload.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int CleanPayload()
    {
        var writer = new ArrayBufferWriter<byte>(_clean.Length);
        HtmlEscape.EscapeText(_clean, writer);
        return writer.WrittenCount;
    }

    /// <summary>Escape pass over the punctuation-heavy payload.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int HeavyPayload()
    {
        var writer = new ArrayBufferWriter<byte>(_heavy.Length * 2);
        HtmlEscape.EscapeText(_heavy, writer);
        return writer.WrittenCount;
    }
}
