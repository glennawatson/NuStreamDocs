// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Search.Lunr;
using NuStreamDocs.Search.Pagefind;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-page <c>WriteHeadExtra</c> cost for both search engines. Called once per rendered page,
/// so micro-regressions multiply across a 14 K-page corpus.
/// </summary>
/// <remarks>
/// The Pagefind path emits two script tags (loader + bind glue) and skips the
/// <c>nustreamdocs:search-index</c> meta. The Lunr path emits the meta plus two script tags.
/// Both also conditionally emit the section-priority meta — covered by a separate parameter
/// set to keep the with/without distinction visible.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class SearchPluginHeadExtraBenchmarks
{
    /// <summary>Initial sink capacity — sized to comfortably fit the head fragment without resize.</summary>
    private const int SinkCapacity = 512;

    /// <summary>Section-priority string that triggers the optional <c>nustreamdocs:search-section-priorities</c> meta tag.</summary>
    private static readonly byte[] SectionPriorities = "guide/:80,api/:-200"u8.ToArray();

    /// <summary>Pagefind plugin with no section priorities — minimum-work head.</summary>
    private PagefindSearchPlugin _pagefindBare = null!;

    /// <summary>Pagefind plugin with the optional section-priority meta tag.</summary>
    private PagefindSearchPlugin _pagefindWithSections = null!;

    /// <summary>Lunr plugin with no section priorities.</summary>
    private LunrSearchPlugin _lunrBare = null!;

    /// <summary>Lunr plugin with the optional section-priority meta tag.</summary>
    private LunrSearchPlugin _lunrWithSections = null!;

    /// <summary>Reused sink so per-iteration allocation reflects the head writer, not the buffer.</summary>
    private ArrayBufferWriter<byte> _sink = null!;

    /// <summary>Builds plugin instances + a reused sink once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pagefindBare = new();
        _pagefindWithSections = new(PagefindOptions.Default with { SectionPriorities = SectionPriorities });
        _lunrBare = new();
        _lunrWithSections = new(LunrOptions.Default with { SectionPriorities = SectionPriorities });
        _sink = new(SinkCapacity);
    }

    /// <summary>Pagefind head-extras with no section-priority meta — minimum work path.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark(Baseline = true)]
    public int PagefindHeadBare()
    {
        _sink.ResetWrittenCount();
        _pagefindBare.WriteHeadExtra(_sink);
        return _sink.WrittenCount;
    }

    /// <summary>Pagefind head-extras with the optional section-priority meta tag.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int PagefindHeadWithSections()
    {
        _sink.ResetWrittenCount();
        _pagefindWithSections.WriteHeadExtra(_sink);
        return _sink.WrittenCount;
    }

    /// <summary>Lunr head-extras with no section-priority meta — emits the manifest discovery meta + runtime + glue.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int LunrHeadBare()
    {
        _sink.ResetWrittenCount();
        _lunrBare.WriteHeadExtra(_sink);
        return _sink.WrittenCount;
    }

    /// <summary>Lunr head-extras with the optional section-priority meta tag.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int LunrHeadWithSections()
    {
        _sink.ResetWrittenCount();
        _lunrWithSections.WriteHeadExtra(_sink);
        return _sink.WrittenCount;
    }
}
