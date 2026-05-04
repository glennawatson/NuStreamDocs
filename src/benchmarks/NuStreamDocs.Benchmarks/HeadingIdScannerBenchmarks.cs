// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Autorefs;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-page cost of <see cref="HeadingIdScanner.ScanAndRegister"/> across
/// a matrix of page sizes and heading densities.
/// </summary>
/// <remarks>
/// The autorefs heading scan runs for every rendered page when an
/// <see cref="AutorefsRegistry"/> is in play, so the per-page cost
/// multiplies through large corpora. The benchmark shapes match the
/// rxui mix: a few-KB page with one or two headings is the common case;
/// the longer/denser shapes catch the outliers (long blog posts, API
/// reference dumps).
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class HeadingIdScannerBenchmarks
{
    /// <summary>Bytes per kilobyte; used to convert the <c>PageSizeKb</c> param into a byte count.</summary>
    private const int BytesPerKilobyte = 1024;

    /// <summary>Approximate length of one filler chunk appended to the synthetic page body.</summary>
    private const int FillerChunkBytes = 64;

    /// <summary>Reserve bytes per heading for the heading-tag plus surrounding markup.</summary>
    private const int HeadingReserveBytes = 32;

    /// <summary>Tiny-page size — matches a doc-stub or short prose page.</summary>
    private const int TinyPageKb = 2;

    /// <summary>Mid-page size — matches a typical guide chapter on the rxui corpus.</summary>
    private const int MidPageKb = 16;

    /// <summary>Large-page size — matches a long blog post or reference dump.</summary>
    private const int LargePageKb = 64;

    /// <summary>No headings — exercises the early-out path.</summary>
    private const int NoHeadings = 0;

    /// <summary>Few headings — typical guide page with a couple of sections.</summary>
    private const int FewHeadings = 4;

    /// <summary>Many headings — long reference page or API surface dump.</summary>
    private const int ManyHeadings = 32;

    /// <summary>Page-relative URL bytes shared across every <c>Register</c> call — encoded once per build, exactly as the production plugin does.</summary>
    private static readonly byte[] PageUrl = "guide/intro.html"u8.ToArray();

    /// <summary>Synthetic rendered-HTML payload sized by <see cref="PageSizeKb"/> and <see cref="HeadingCount"/>.</summary>
    private byte[] _html = [];

    /// <summary>Registry rebuilt before every iteration so dictionary growth is part of the measurement.</summary>
    private AutorefsRegistry _registry = null!;

    /// <summary>Gets or sets the rendered-page size in kilobytes.</summary>
    [Params(TinyPageKb, MidPageKb, LargePageKb)]
    public int PageSizeKb { get; set; }

    /// <summary>Gets or sets the number of <c>&lt;hN id="..."&gt;</c> tags spread evenly through the page.</summary>
    [Params(NoHeadings, FewHeadings, ManyHeadings)]
    public int HeadingCount { get; set; }

    /// <summary>Builds the synthetic HTML payload once per param set.</summary>
    [GlobalSetup]
    public void Setup() => _html = BuildHtml(PageSizeKb * BytesPerKilobyte, HeadingCount);

    /// <summary>Resets the registry per iteration so insertion cost is measured fresh.</summary>
    [IterationSetup]
    public void IterationSetup() => _registry = new();

    /// <summary>Scans the synthetic page and registers every heading id.</summary>
    /// <returns>Final registry entry count.</returns>
    [Benchmark]
    public int ScanAndRegister()
    {
        HeadingIdScanner.ScanAndRegister(_html, PageUrl, _registry);
        return _registry.Count;
    }

    /// <summary>Builds an HTML byte payload of <paramref name="byteLength"/> with <paramref name="headingCount"/> headings spread evenly.</summary>
    /// <param name="byteLength">Target byte length.</param>
    /// <param name="headingCount">Number of headings to embed.</param>
    /// <returns>UTF-8 byte payload.</returns>
    private static byte[] BuildHtml(int byteLength, int headingCount)
    {
        var sb = new StringBuilder(byteLength + (headingCount * HeadingReserveBytes));
        var fillerSegmentBytes = headingCount > 0 ? byteLength / (headingCount + 1) : byteLength;

        for (var i = 0; i <= headingCount; i++)
        {
            AppendFiller(sb, fillerSegmentBytes);
            if (i >= headingCount)
            {
                continue;
            }

            sb.Append("<h2 id=\"section-")
                .Append(i.ToString(CultureInfo.InvariantCulture))
                .Append("\">Section ")
                .Append(i.ToString(CultureInfo.InvariantCulture))
                .Append("</h2>");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Appends a chunk of plain HTML body bytes — paragraph runs interleaved with anchor tags so the scanner walks past plenty of <c>&lt;</c> bytes.</summary>
    /// <param name="sb">Accumulator.</param>
    /// <param name="approximateLength">Approximate byte target for the chunk.</param>
    private static void AppendFiller(StringBuilder sb, int approximateLength)
    {
        var written = 0;
        while (written < approximateLength)
        {
            sb.Append("<p>The lazy dog jumps over <a href=\"#x\">the box</a> in the meadow.</p>");
            written += FillerChunkBytes;
        }
    }
}
