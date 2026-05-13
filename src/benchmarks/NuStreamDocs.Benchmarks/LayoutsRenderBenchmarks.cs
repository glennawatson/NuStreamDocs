// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Layouts;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks isolating the <see cref="TemplateCache"/> hit/miss/no-cache paths in <see cref="LayoutRenderer"/>.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class LayoutsRenderBenchmarks
{
    /// <summary>Number of pages simulated by the multi-page benchmarks.</summary>
    private const int PageRepetitions = 200;

    /// <summary>Default include / extends recursion cap.</summary>
    private const int MaxDepth = 8;

    /// <summary>Initial sink capacity — wide enough to hold the rendered page without re-growing during the measurement.</summary>
    private const int SinkCapacity = 4096;

    /// <summary>Page-template body used by every benchmark fixture.</summary>
    private const string PageTemplate = "<!doctype html>"
                                        + "<html><head><title>{{ page.title }}</title></head>"
                                        + "<body>{% include \"header.html\" %}"
                                        + "{% block body %}<main>{{ page.content }}</main>{% endblock %}"
                                        + "</body></html>";

    /// <summary>Header-include body.</summary>
    private const string HeaderTemplate = "<header>Site</header>";

    /// <summary>Temp directory for the layout fixture; lifetime-bound to the benchmark instance.</summary>
    private string _root = string.Empty;

    /// <summary>Page source bytes (frontmatter only — body text isn't needed because <see cref="LayoutContext"/> takes the rendered HTML separately).</summary>
    private byte[] _source = [];

    /// <summary>Pre-rendered HTML body shared across iterations.</summary>
    private byte[] _html = [];

    /// <summary>Resolved template directory.</summary>
    private DirectoryPath _templateDir;

    /// <summary>UTF-8 template name (<c>page.html</c>).</summary>
    private byte[] _templateName = [];

    /// <summary>Cache reused by warm-cache benchmarks; pre-populated in iteration setup.</summary>
    private TemplateCache _warmCache = new();

    /// <summary>Per-iteration sink. Reset between invocations.</summary>
    private ArrayBufferWriter<byte> _sink = new(SinkCapacity);

    /// <summary>Writes the layout fixture and pre-builds the page bytes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _root = Directory.CreateTempSubdirectory("layouts-bench").FullName;
        File.WriteAllBytes(Path.Combine(_root, "page.html"), Encoding.UTF8.GetBytes(PageTemplate));
        File.WriteAllBytes(Path.Combine(_root, "header.html"), Encoding.UTF8.GetBytes(HeaderTemplate));

        _templateDir = _root;
        _templateName = "page.html"u8.ToArray();
        _source = [.. "---\ntemplate: page.html\ntitle: Hello\n---\n"u8];
        _html = [.. "<p>Body content here, large enough to register on the writer.</p>"u8];
        _sink = new(SinkCapacity);
        _warmCache = new();
    }

    /// <summary>Cleans up the temp directory.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        Directory.Delete(_root, true);
    }

    /// <summary>Pre-populates <see cref="_warmCache"/> by performing one full render against it before every warm-cache benchmark iteration.</summary>
    [IterationSetup(Targets = [nameof(Render_Warm_WithCache), nameof(Render_200Pages_WithCache)])]
    public void WarmCacheSetup()
    {
        _warmCache = new();
        _sink = new(SinkCapacity);
        var ctx = BuildLayoutContext();
        LayoutRenderer.Render(_templateName, _templateDir, ctx, MaxDepth, _sink, NullLogger.Instance, _warmCache);
        _sink = new(SinkCapacity);
    }

    /// <summary>Resets the sink before benchmarks that don't use the warm-cache iteration setup.</summary>
    [IterationSetup(Targets =
        [nameof(Render_Cold_NoCache), nameof(Render_Cold_WithCache), nameof(Render_200Pages_NoCache)])]
    public void ColdSetup() => _sink = new(SinkCapacity);

    /// <summary>Single render with no cache (every call re-reads + re-parses templates from disk).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Render_Cold_NoCache()
    {
        var ctx = BuildLayoutContext();
        LayoutRenderer.Render(_templateName, _templateDir, ctx, MaxDepth, _sink, NullLogger.Instance, null);
        return _sink.WrittenCount;
    }

    /// <summary>Single render with a fresh empty cache — measures the miss path including the cache write.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Render_Cold_WithCache()
    {
        TemplateCache cache = new();
        var ctx = BuildLayoutContext();
        LayoutRenderer.Render(_templateName, _templateDir, ctx, MaxDepth, _sink, NullLogger.Instance, cache);
        return _sink.WrittenCount;
    }

    /// <summary>Single render against a pre-warmed cache — the headline cache-hit number.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Render_Warm_WithCache()
    {
        var ctx = BuildLayoutContext();
        LayoutRenderer.Render(_templateName, _templateDir, ctx, MaxDepth, _sink, NullLogger.Instance, _warmCache);
        return _sink.WrittenCount;
    }

    /// <summary>Renders 200 pages with no cache (every page re-loads and re-parses both templates).</summary>
    /// <returns>Bytes written across all pages.</returns>
    [Benchmark]
    public int Render_200Pages_NoCache()
    {
        var ctx = BuildLayoutContext();
        for (var i = 0; i < PageRepetitions; i++)
        {
            LayoutRenderer.Render(_templateName, _templateDir, ctx, MaxDepth, _sink, NullLogger.Instance, null);
        }

        return _sink.WrittenCount;
    }

    /// <summary>Renders 200 pages against a pre-warmed cache — simulates a real build where many pages share one template.</summary>
    /// <returns>Bytes written across all pages.</returns>
    [Benchmark]
    public int Render_200Pages_WithCache()
    {
        var ctx = BuildLayoutContext();
        for (var i = 0; i < PageRepetitions; i++)
        {
            LayoutRenderer.Render(_templateName, _templateDir, ctx, MaxDepth, _sink, NullLogger.Instance, _warmCache);
        }

        return _sink.WrittenCount;
    }

    /// <summary>Builds a fresh <see cref="LayoutContext"/> for each measured render.</summary>
    /// <returns>Populated context.</returns>
    private LayoutContext BuildLayoutContext() => LayoutContext.FromPage(_source, _html, "page.html"u8);
}
