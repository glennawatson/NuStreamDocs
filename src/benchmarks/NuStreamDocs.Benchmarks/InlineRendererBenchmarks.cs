// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the inline renderer alone.</summary>
/// <remarks>
/// Drives <c>InlineRenderer.Render</c> against a pre-built body
/// of inline content (links, code, emphasis, autolinks, hard breaks).
/// Isolates the inline-emit cost from the block scan and the HTML wrap.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class InlineRendererBenchmarks
{
    /// <summary>Number of times the fixture line is repeated.</summary>
    private const int Repetitions = 500;

    /// <summary>Headroom factor for the output writer (HTML expansion ≈ 1.6× for typical inline content; 2× is generous).</summary>
    private const int OutputExpansionFactor = 2;

    /// <summary>Pre-built UTF-8 inline source.</summary>
    private byte[] _source = [];

    /// <summary>Reused output writer; pre-sized once in <c>Setup</c> and reset per iteration to mirror the per-thread cache pattern in production callers.</summary>
    private ArrayBufferWriter<byte> _writer = null!;

    /// <summary>Generates the input UTF-8 buffer once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Repetitions; i++)
        {
            sb.Append("Line with **bold**, *italic*, `code`, a [link](https://x/")
              .Append(i)
              .Append(") and an autolink <https://y/")
              .Append(i)
              .Append("> plus an escaped \\* asterisk.\n");
        }

        _source = Encoding.UTF8.GetBytes(sb.ToString());
        _writer = new(_source.Length * OutputExpansionFactor);
    }

    /// <summary>Pure inline render into a reused writer; isolates the renderer cost from one-shot writer growth.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Render()
    {
        _writer.ResetWrittenCount();
        InlineRenderer.Render(_source, _writer);
        return _writer.WrittenCount;
    }
}
