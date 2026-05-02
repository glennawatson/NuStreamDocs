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
[ShortRunJob]
[MemoryDiagnoser]
public class HtmlEscapeBenchmarks
{
    /// <summary>Repetitions to grow the input enough to time.</summary>
    private const int Repetitions = 2000;

    /// <summary>Headroom factor for the heavy-payload writer (~10% expansion from entity replacements; 2× is generous).</summary>
    private const int HeavyExpansionFactor = 2;

    /// <summary>Pre-built clean buffer (no escapable bytes).</summary>
    private byte[] _clean = [];

    /// <summary>Pre-built buffer where every line contains escapable bytes.</summary>
    private byte[] _heavy = [];

    /// <summary>Reused writer for the clean-payload benchmark — constructed once so the per-invocation measurement reflects the actual escape path, not an LOH-bound writer alloc.</summary>
    private ArrayBufferWriter<byte> _cleanWriter = null!;

    /// <summary>Reused writer for the heavy-payload benchmark.</summary>
    private ArrayBufferWriter<byte> _heavyWriter = null!;

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
        _cleanWriter = new(_clean.Length);
        _heavyWriter = new(_heavy.Length * HeavyExpansionFactor);
    }

    /// <summary>Escape pass over the no-escapes payload, reusing the writer across invocations.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int CleanPayload()
    {
        _cleanWriter.ResetWrittenCount();
        HtmlEscape.EscapeText(_clean, _cleanWriter);
        return _cleanWriter.WrittenCount;
    }

    /// <summary>Escape pass over the punctuation-heavy payload, reusing the writer across invocations.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int HeavyPayload()
    {
        _heavyWriter.ResetWrittenCount();
        HtmlEscape.EscapeText(_heavy, _heavyWriter);
        return _heavyWriter.WrittenCount;
    }

    /// <summary>Attribute-escape pass over the clean payload — only <c>&amp;</c>/<c>"</c> trigger replacement, so this measures the IndexOfAny fast path against a smaller search-set.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int AttributeCleanPayload()
    {
        _cleanWriter.ResetWrittenCount();
        HtmlEscape.EscapeAttribute(_clean, _cleanWriter);
        return _cleanWriter.WrittenCount;
    }

    /// <summary>Attribute-escape pass over the punctuation-heavy payload — exercises only the <c>&amp;</c>/<c>"</c> branches; <c>&lt;</c>/<c>&gt;</c> are copied verbatim.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int AttributeHeavyPayload()
    {
        _heavyWriter.ResetWrittenCount();
        HtmlEscape.EscapeAttribute(_heavy, _heavyWriter);
        return _heavyWriter.WrittenCount;
    }
}
