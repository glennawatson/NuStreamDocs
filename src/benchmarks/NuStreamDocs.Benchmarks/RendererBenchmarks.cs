// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <c>MarkdownRenderer</c>.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class RendererBenchmarks
{
    /// <summary>Small synthetic-document size used as a baseline.</summary>
    private const int SmallParagraphs = 100;

    /// <summary>Medium synthetic-document size; matches a typical doc page count.</summary>
    private const int MediumParagraphs = 1000;

    /// <summary>Headroom factor for the output writer (HTML expansion ≈ 1.6× for typical prose; 2× is generous).</summary>
    private const int OutputExpansionFactor = 2;

    /// <summary>Pre-built UTF-8 input buffer; populated in <c>Setup</c>.</summary>
    private byte[] _source = [];

    /// <summary>Reused output writer, sized once in <c>Setup</c> and reset per iteration.</summary>
    /// <remarks>Mirrors the per-thread cache that <c>MarkdownRenderer</c> uses in production callers.</remarks>
    private ArrayBufferWriter<byte> _writer = null!;

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
        _writer = new(_source.Length * OutputExpansionFactor);
    }

    /// <summary>Full render into a reused writer; isolates the renderer cost from one-shot writer growth.</summary>
    /// <returns>Bytes written, returned to keep the JIT honest.</returns>
    [Benchmark]
    public int Render()
    {
        _writer.ResetWrittenCount();
        MarkdownRenderer.Render(_source, _writer);
        return _writer.WrittenCount;
    }
}
