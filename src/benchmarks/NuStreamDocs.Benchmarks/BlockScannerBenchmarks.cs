// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the block-level scanner alone (no inline render, no HTML emit).</summary>
/// <remarks>
/// Isolates the cost of the structural pass — fence detection, paragraph
/// boundaries, list / blockquote scope tracking — from inline + HTML emit.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class BlockScannerBenchmarks
{
    /// <summary>Small synthetic-document size used as a baseline.</summary>
    private const int SmallParagraphs = 100;

    /// <summary>Medium synthetic-document size; matches a typical doc page count.</summary>
    private const int MediumParagraphs = 1000;

    /// <summary>Average input bytes per emitted block in the synthetic corpus — drives the writer's initial capacity hint so the steady-state run never grows past the LOH threshold.</summary>
    private const int BytesPerBlockHint = 13;

    /// <summary>Pre-built UTF-8 input buffer; populated in <c>Setup</c>.</summary>
    private byte[] _source = [];

    /// <summary>Reused block-span writer, sized once in <c>Setup</c> and reset per iteration.</summary>
    /// <remarks>Mirrors the per-thread cache that <c>MarkdownRenderer</c> uses in production.</remarks>
    private ArrayBufferWriter<BlockSpan> _writer = null!;

    /// <summary>Pre-built UTF-8 input containing many same-line HTML comments that exercise the same-line-close branch.</summary>
    private byte[] _commentSource = [];

    /// <summary>Gets or sets the number of paragraphs in the synthetic input document.</summary>
    [Params(SmallParagraphs, MediumParagraphs)]
    public int Paragraphs { get; set; }

    /// <summary>Generates the input UTF-8 buffer once per parameter set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        StringBuilder sb = new();
        for (var i = 0; i < Paragraphs; i++)
        {
            sb.Append("# Heading ").Append(i).Append('\n')
              .Append("Paragraph with `inline code`, a [link](https://x/").Append(i).Append(") and a > quote.\n\n")
              .Append("- list item ").Append(i).Append('\n')
              .Append("- list item B\n\n")
              .Append("```\nfenced\n```\n\n");
        }

        _source = Encoding.UTF8.GetBytes(sb.ToString());
        _writer = new(Math.Max(_source.Length / BytesPerBlockHint, 1));
    }

    /// <summary>Pure block-scan into a reused <c>BlockSpan</c> writer; isolates the scanner cost from one-shot writer growth.</summary>
    /// <returns>The number of blocks scanned.</returns>
    [Benchmark]
    public int Scan()
    {
        _writer.ResetWrittenCount();
        return BlockScanner.Scan(_source, _writer);
    }

    /// <summary>Block-scan over a comment-heavy synthetic doc; exercises <c>CheckSameLineHtmlBlockClose</c> for every paragraph.</summary>
    /// <returns>The number of blocks scanned.</returns>
    [Benchmark]
    public int ScanCommentHeavy()
    {
        if (_commentSource.Length is 0)
        {
            BuildCommentFixture();
        }

        _writer.ResetWrittenCount();
        return BlockScanner.Scan(_commentSource, _writer);
    }

    /// <summary>Builds the comment-heavy fixture lazily on first call so the parameter sweep only pays for it when needed.</summary>
    private void BuildCommentFixture()
    {
        StringBuilder sb = new();
        for (var i = 0; i < Paragraphs; i++)
        {
            sb.Append("<!-- single-line note ").Append(i).Append(" -->\n")
              .Append("## Heading ").Append(i).Append('\n')
              .Append("<!--- triple-dash form ").Append(i).Append(" --->\n")
              .Append("Body paragraph ").Append(i).Append(".\n\n");
        }

        _commentSource = Encoding.UTF8.GetBytes(sb.ToString());
    }
}
