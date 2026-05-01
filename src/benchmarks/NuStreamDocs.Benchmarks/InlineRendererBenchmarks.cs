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
[MemoryDiagnoser]
public class InlineRendererBenchmarks
{
    /// <summary>Number of times the fixture line is repeated.</summary>
    private const int Repetitions = 500;

    /// <summary>Pre-built UTF-8 inline source.</summary>
    private byte[] _source = [];

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
    }

    /// <summary>Pure inline render to a pooled <c>ArrayBufferWriter{T}</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Render()
    {
        var writer = new ArrayBufferWriter<byte>(_source.Length * 2);
        InlineRenderer.Render(_source, writer);
        return writer.WrittenCount;
    }
}
