// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <c>MarkdownRenderer</c>.</summary>
[MemoryDiagnoser]
public class RendererBenchmarks
{
    /// <summary>Small synthetic-document size used as a baseline.</summary>
    private const int SmallParagraphs = 100;

    /// <summary>Medium synthetic-document size; matches a typical doc page count.</summary>
    private const int MediumParagraphs = 1000;

    /// <summary>Pre-built UTF-8 input buffer; populated in <c>Setup</c>.</summary>
    private byte[] _source = [];

    /// <summary>Gets or sets the number of paragraphs in the synthetic input document.</summary>
    [Params(SmallParagraphs, MediumParagraphs)]
    public int Paragraphs { get; set; }

    /// <summary>Generates the input UTF-8 buffer once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Paragraphs; i++)
        {
            sb.Append("# Heading ").Append(i).Append('\n')
              .Append("This is a paragraph with some text & a < b.\n\n");
        }

        _source = Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Full render to a pooled <c>ArrayBufferWriter{T}</c>.</summary>
    /// <returns>Bytes written, returned to keep the JIT honest.</returns>
    [Benchmark]
    public int Render()
    {
        var writer = new ArrayBufferWriter<byte>(_source.Length * 2);
        MarkdownRenderer.Render(_source, writer);
        return writer.WrittenCount;
    }
}
