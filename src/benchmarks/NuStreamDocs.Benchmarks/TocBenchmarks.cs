// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Plugins;
using NuStreamDocs.Toc;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the TOC plugin's heading scan, rewrite, and fragment-render passes.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class TocBenchmarks
{
    /// <summary>Small heading-count parameter — exercises the cheap scan path.</summary>
    private const int SmallHeadings = 10;

    /// <summary>Medium heading-count parameter — typical doc-page size.</summary>
    private const int MediumHeadings = 50;

    /// <summary>Large heading-count parameter — stress for the renderer.</summary>
    private const int LargeHeadings = 200;

    /// <summary>Pre-built HTML fixture sized by <c>HeadingCount</c>.</summary>
    private byte[] _html = [];

    /// <summary>Pre-built scan result so the rewrite/fragment benchmarks don't repeat the scan cost.</summary>
    private Heading[] _slugged = [];

    /// <summary>Pre-built scan result without slugs assigned, used by the AssignSlugs micro-benchmark.</summary>
    private Heading[] _scanned = [];

    /// <summary>Gets or sets the synthetic heading count for the current iteration.</summary>
    [Params(SmallHeadings, MediumHeadings, LargeHeadings)]
    public int HeadingCount { get; set; }

    /// <summary>Generates the HTML fixture for the current <c>HeadingCount</c>.</summary>
    [GlobalSetup]
    public void Setup()
    {
        StringBuilder sb = new(HeadingCount * 64);
        sb.Append("<aside><!--@@toc@@--></aside>");
        for (var i = 0; i < HeadingCount; i++)
        {
            var level = 2 + (i % 3);
            sb.Append("<h").Append(level).Append('>')
                .Append("Section ").Append(i)
                .Append("</h").Append(level).Append('>')
                .Append("<p>Body ").Append(i).Append("</p>");
        }

        _html = Encoding.UTF8.GetBytes(sb.ToString());
        _scanned = HeadingScanner.Scan(_html);
        _slugged = HeadingSlugifier.AssignSlugs(_html, _scanned).Slugged;
    }

    /// <summary>Benchmark for <c>HeadingSlugifier.AssignSlugs</c> — exercises text decode + byte slugify + dedup map for the whole page.</summary>
    /// <returns>The slugged heading count.</returns>
    [Benchmark]
    public int AssignSlugs() => HeadingSlugifier.AssignSlugs(_html, _scanned).Slugged.Length;

    /// <summary>Benchmark for the byte-only <c>SlugifyToBytes</c> hot path used by <c>AssignSlugs</c> for headings without an existing id.</summary>
    /// <returns>The slug byte length.</returns>
    [Benchmark]
    public int SlugifyToBytes() => HeadingSlugifier.SlugifyToBytes("Some Heading Title 42"u8).Length;

    /// <summary>Benchmark for <c>HeadingScanner.Scan(ReadOnlySpan{byte})</c>.</summary>
    /// <returns>The scanned heading count.</returns>
    [Benchmark]
    public int Scan() => HeadingScanner.Scan(_html).Length;

    /// <summary>Benchmark for <c>HeadingRewriter.Rewrite</c>.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int Rewrite()
    {
        ArrayBufferWriter<byte> sink = new(_html.Length * 2);
        HeadingRewriter.Rewrite(_html, _slugged, "¶"u8, sink);
        return sink.WrittenCount;
    }

    /// <summary>Benchmark for <c>TocFragmentRenderer.Render</c>.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int Fragment()
    {
        ArrayBufferWriter<byte> sink = new(512);
        var opts = TocOptions.Default;
        TocFragmentRenderer.Render(_html, _slugged, in opts, sink);
        return sink.WrittenCount;
    }

    /// <summary>End-to-end benchmark of <c>TocPlugin.PostRender</c>.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public int OnRenderPage()
    {
        ArrayBufferWriter<byte> sink = new(_html.Length * 2);
        TocPlugin plugin = new();
        PagePostRenderContext ctx = new("page.md", default, _html, sink);
        plugin.PostRender(in ctx);
        return sink.WrittenCount;
    }
}
