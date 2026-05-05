// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.LinkValidator;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-page cost of the byte-only <c>LinkExtractor</c> entry points
/// across a matrix of page sizes and link densities. The validator
/// runs these scans for every emitted .html file at finalize, so the
/// per-page cost multiplies through large corpora; this benchmark
/// matches the shapes seen on the rxui corpus.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class LinkExtractorBenchmarks
{
    /// <summary>Bytes per kilobyte; used to convert the <c>PageSizeKb</c> param into a byte count.</summary>
    private const int BytesPerKb = 1024;

    /// <summary>Smallest synthesized page (~2 KB) — typical short doc page.</summary>
    private const int SmallPageKb = 2;

    /// <summary>Mid-range synthesized page (~16 KB) — typical guide / blog post.</summary>
    private const int MediumPageKb = 16;

    /// <summary>Largest synthesized page (~64 KB) — long-form API reference.</summary>
    private const int LargePageKb = 64;

    /// <summary>Sparse link density.</summary>
    private const int LowDensity = 2;

    /// <summary>Typical link density seen in mkdocs-material guides.</summary>
    private const int MediumDensity = 8;

    /// <summary>Heavy link density seen on API-reference pages.</summary>
    private const int HighDensity = 16;

    /// <summary>Number of element shapes the synthesizer cycles through (internal / external / asset / image-src / heading-id).</summary>
    private const int ElementShapes = 5;

    /// <summary>Pre-built page bytes for the current iteration.</summary>
    private byte[] _html = [];

    /// <summary>Gets or sets the synthetic page size in kilobytes.</summary>
    [Params(SmallPageKb, MediumPageKb, LargePageKb)]
    public int PageSizeKb { get; set; }

    /// <summary>Gets or sets the synthetic link density (links per ~200 bytes of body text).</summary>
    [Params(LowDensity, MediumDensity, HighDensity)]
    public int LinksPer200Bytes { get; set; }

    /// <summary>Generates the HTML fixture for the current params.</summary>
    [GlobalSetup]
    public void Setup()
    {
        StringBuilder sb = new(PageSizeKb * BytesPerKb);
        var totalBytes = PageSizeKb * BytesPerKb;
        var blockEvery = 200 / Math.Max(1, LinksPer200Bytes);
        var idx = 0;
        var written = 0;
        while (written < totalBytes)
        {
            // Mix of internal, external, asset, image, and heading-id shapes — the cross-section the validator sees on a real page.
            var i = idx.ToString(CultureInfo.InvariantCulture);
            var emitted = (idx % ElementShapes) switch
            {
                0 => $"<a href=\"page{i}.html\">link {i}</a>",
                1 => $"<a href=\"https://example{i}.com\">ext {i}</a>",
                2 => $"<a href=\"image{i}.png\">img {i}</a>",
                3 => $"<img src=\"https://cdn.test/x{i}.jpg\" />",
                _ => $"<h2 id=\"section-{i}\">Section {i}</h2>"
            };
            sb.Append(emitted);
            written += emitted.Length;

            // Filler text between elements so links aren't packed back-to-back.
            for (var f = 0; f < blockEvery && written < totalBytes; f++)
            {
                const string Filler = "<p>Lorem ipsum dolor sit amet.</p>";
                sb.Append(Filler);
                written += Filler.Length;
            }

            idx++;
        }

        _html = Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Benchmark for byte-only href scan.</summary>
    /// <returns>The captured href count.</returns>
    [Benchmark]
    public int ExtractHrefRanges() => LinkExtractor.ExtractHrefRanges(_html).Length;

    /// <summary>Benchmark for byte-only src scan.</summary>
    /// <returns>The captured src count.</returns>
    [Benchmark]
    public int ExtractSrcRanges() => LinkExtractor.ExtractSrcRanges(_html).Length;

    /// <summary>Benchmark for the heading-only id scan.</summary>
    /// <returns>The captured heading-id count.</returns>
    [Benchmark]
    public int ExtractHeadingIdRanges() => LinkExtractor.ExtractHeadingIdRanges(_html).Length;
}
