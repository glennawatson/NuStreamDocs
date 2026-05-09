// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Plugins;
using NuStreamDocs.Search.Lunr;
using NuStreamDocs.Search.Pagefind;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-page <c>Scan</c> path through <c>SearchPluginBase</c> — exercised once per rendered page
/// during the parallel render stage, so a regression here is the most obvious place to see a
/// site-wide build slowdown.
/// </summary>
/// <remarks>
/// Both engines share the base scan path; we still run them as separate benchmarks so any
/// future per-engine override (e.g. a Lunr-only stopword filter) gets caught individually.
/// Two payload sizes: short (~300 bytes of HTML, no frontmatter) and long (~30 KB of HTML
/// with frontmatter keys folded in) — to bracket the realistic per-page corpus.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class SearchPluginScanBenchmarks
{
    /// <summary>Repetitions used to grow the long HTML payload past the page-builder pool's small-input fast path.</summary>
    private const int LongPayloadRepeats = 600;

    /// <summary>Page path supplied to every Scan call so the title-fallback branch never trips.</summary>
    private const string RelativePath = "guide/intro.md";

    /// <summary>Pre-built short HTML payload (~300 bytes).</summary>
    private byte[] _shortHtml = [];

    /// <summary>Pre-built long HTML payload (~30 KB, exercises the byte walker).</summary>
    private byte[] _longHtml = [];

    /// <summary>Frontmatter source bytes used for the with-frontmatter benchmark.</summary>
    private byte[] _frontmatterSource = [];

    /// <summary>Pagefind plugin with no frontmatter keys configured.</summary>
    private PagefindSearchPlugin _pagefindPlain = null!;

    /// <summary>Pagefind plugin with three frontmatter keys configured.</summary>
    private PagefindSearchPlugin _pagefindWithFrontmatter = null!;

    /// <summary>Lunr plugin with default options — same scan path as Pagefind, isolated for divergence.</summary>
    private LunrSearchPlugin _lunrPlain = null!;

    /// <summary>Builds the input payloads + plugin instances once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _shortHtml = "<h1>Intro</h1><p>hello world content for the search index</p>"u8.ToArray();

        StringBuilder html = new((LongPayloadRepeats * 50) + 32);
        html.Append("<h1>Long Page Title</h1>");
        for (var i = 0; i < LongPayloadRepeats; i++)
        {
            html.Append("<p>Paragraph ").Append(i).Append(" — searchable body content with keywords.</p>");
        }

        _longHtml = Encoding.UTF8.GetBytes(html.ToString());

        _frontmatterSource = "---\ntags: [foo, bar]\nauthor: Glenn\nsummary: A short summary line\n---\nbody"u8.ToArray();

        _pagefindPlain = new();
        _pagefindWithFrontmatter = new(PagefindOptions.Default with
        {
            SearchableFrontmatterKeys = [[.. "tags"u8], [.. "author"u8], [.. "summary"u8]],
        });
        _lunrPlain = new();
    }

    /// <summary>Pagefind scan over a small HTML payload — measures the page-builder rental + tag-stripper hot path.</summary>
    [Benchmark(Baseline = true)]
    public void PagefindScanShort()
    {
        PageScanContext ctx = new(RelativePath, default, _shortHtml);
        _pagefindPlain.Scan(in ctx);
    }

    /// <summary>Lunr scan over the same payload — should be indistinguishable from Pagefind (both go through the base).</summary>
    [Benchmark]
    public void LunrScanShort()
    {
        PageScanContext ctx = new(RelativePath, default, _shortHtml);
        _lunrPlain.Scan(in ctx);
    }

    /// <summary>Pagefind scan over a long payload — exercises the inner per-byte walker against a realistic page size.</summary>
    [Benchmark]
    public void PagefindScanLong()
    {
        PageScanContext ctx = new(RelativePath, default, _longHtml);
        _pagefindPlain.Scan(in ctx);
    }

    /// <summary>Pagefind scan with three frontmatter keys folded into the document text — measures the AppendKeysTo cost on top of the bare scan.</summary>
    [Benchmark]
    public void PagefindScanWithFrontmatter()
    {
        PageScanContext ctx = new(RelativePath, _frontmatterSource, _shortHtml);
        _pagefindWithFrontmatter.Scan(in ctx);
    }
}
