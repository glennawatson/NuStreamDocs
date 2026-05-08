// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.LinkValidator;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the link-validator hot path: per-page href/id scan and full-corpus internal validation.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class LinkValidatorBenchmarks
{
    /// <summary>Small per-page link count exercising the cheap scan path.</summary>
    private const int SmallLinks = 25;

    /// <summary>Medium per-page link count typical of doc pages.</summary>
    private const int MediumLinks = 100;

    /// <summary>Validator parallelism for the corpus benchmark; matches LinkValidatorOptions.Default.</summary>
    private const int Parallelism = 4;

    /// <summary>Corpus page count used by the full-validate benchmark.</summary>
    private const int CorpusPages = 200;

    /// <summary>Pre-built UTF-8 HTML for the per-page scan benchmark.</summary>
    private byte[] _pageHtml = [];

    /// <summary>Pre-built site-relative URL bytes for the per-page scan benchmark.</summary>
    private byte[] _pageUrl = [];

    /// <summary>Pre-built corpus for the full-validate benchmark.</summary>
    private ValidationCorpus _corpus = null!;

    /// <summary>Gets or sets the synthetic per-page link count for the scan benchmark.</summary>
    [Params(SmallLinks, MediumLinks)]
    public int Links { get; set; }

    /// <summary>Builds one HTML page (for <see cref="ScanPage"/>) and an in-memory page corpus (for <see cref="ValidateCorpus"/>).</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pageHtml = BuildPageHtml(Links);
        _pageUrl = "guide/page.html"u8.ToArray();

        Dictionary<byte[], PageLinks> pages = new(CorpusPages, ByteArrayComparer.Instance);
        for (var i = 0; i < CorpusPages; i++)
        {
            var url = Encoding.UTF8.GetBytes($"guide/page-{i}.html");
            pages[url] = ValidationCorpus.Scan(url, BuildPageHtml(Links));
        }

        _corpus = ValidationCorpus.FromPages(pages);
    }

    /// <summary>Benchmark for <c>ValidationCorpus.Scan</c> — extracts hrefs / ids / src refs from one page.</summary>
    /// <returns>Internal link count (forces the scan to materialize).</returns>
    [Benchmark]
    public int ScanPage() => ValidationCorpus.Scan(_pageUrl, _pageHtml).InternalLinks.Length;

    /// <summary>Benchmark for the full <c>InternalLinkValidator.ValidateAsync</c> over a 200-page corpus.</summary>
    /// <returns>Diagnostic count.</returns>
    [Benchmark]
    public async Task<int> ValidateCorpus()
    {
        var diags = await InternalLinkValidator.ValidateAsync(_corpus, Parallelism, CancellationToken.None).ConfigureAwait(false);
        return diags.Length;
    }

    /// <summary>Synthesizes a UTF-8 HTML page with <paramref name="links"/> internal hrefs and matching heading ids.</summary>
    /// <param name="links">Number of links and headings to embed.</param>
    /// <returns>UTF-8 HTML bytes.</returns>
    private static byte[] BuildPageHtml(int links)
    {
        StringBuilder sb = new(links * 96);
        sb.Append("<article>");
        for (var i = 0; i < links; i++)
        {
            sb.Append("<h2 id=\"section-").Append(i).Append("\">Section ").Append(i).Append("</h2>")
              .Append("<p>See <a href=\"page-").Append(i % CorpusPages).Append(".html#section-").Append(i).Append("\">link ").Append(i).Append("</a> ")
              .Append("and <a href=\"../sibling/page-").Append(i).Append(".html\">sibling</a> ")
              .Append("and <a href=\"https://example.com/external-").Append(i).Append("\">ext</a>.</p>");
        }

        sb.Append("</article>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
