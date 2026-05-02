// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Autorefs;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-call cost of <see cref="AutorefsRegistry.Register(string, string, string)"/> across the
/// two real-world fragment shapes: heading scans (id == fragment so the
/// stored URL is <c>page#id</c>) and whole-page references (no fragment
/// so the stored URL is the bare page URL).
/// </summary>
/// <remarks>
/// On the rxui corpus the registry sees ~138K Register calls per build
/// (one per heading + cross-doc symbol). The fragment branch allocates
/// a concatenated string per call, so isolating that cost from the
/// dictionary-insert and contention costs is what we want here.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class AutorefsRegistryBenchmarks
{
    /// <summary>Headings clustered per synthetic page — matches the rxui mix.</summary>
    private const int HeadingsPerPage = 10;

    /// <summary>Small registry — multi-package library API surface.</summary>
    private const int SmallSite = 1_000;

    /// <summary>Mid-sized registry — typical doc site with one heading per page across thousands of pages.</summary>
    private const int MidSite = 10_000;

    /// <summary>Large registry — rxui-scale heading inventory.</summary>
    private const int LargeSite = 100_000;

    /// <summary>Initial capacity for the resolve sink — generous enough that no per-iteration grow is observed for typical page-relative URLs.</summary>
    private const int ResolveSinkCapacity = 128;

    /// <summary>Page URL pre-allocated once and reused for every call to remove encode-once-per-call noise.</summary>
    private string[] _pageUrls = [];

    /// <summary>IDs pre-allocated once and reused across iterations.</summary>
    private string[] _ids = [];

    /// <summary>UTF-8 page URL bytes — pre-encoded once so the byte-path benches measure dictionary-insert cost only.</summary>
    private byte[][] _pageUrlBytes = [];

    /// <summary>UTF-8 id bytes — pre-encoded once for the byte-path benches.</summary>
    private byte[][] _idBytes = [];

    /// <summary>Reusable sink for the byte-path resolve bench.</summary>
    private ArrayBufferWriter<byte> _resolveSink = null!;

    /// <summary>Registry pre-populated once in setup so the resolve bench measures lookup cost, not insert cost.</summary>
    private AutorefsRegistry _populatedRegistry = null!;

    /// <summary>Registry recreated per iteration so dictionary growth is measured.</summary>
    private AutorefsRegistry _registry = null!;

    /// <summary>Gets or sets the number of registrations per iteration.</summary>
    [Params(SmallSite, MidSite, LargeSite)]
    public int EntryCount { get; set; }

    /// <summary>Pre-allocates id + URL strings shared across iterations, plus the UTF-8 byte forms used by the byte-path benches.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _ids = new string[EntryCount];
        _pageUrls = new string[EntryCount];
        _idBytes = new byte[EntryCount][];
        _pageUrlBytes = new byte[EntryCount][];
        for (var i = 0; i < EntryCount; i++)
        {
            var idx = i.ToString(CultureInfo.InvariantCulture);
            _ids[i] = "section-" + idx;
            _pageUrls[i] = "guide/page-" + (i / HeadingsPerPage).ToString(CultureInfo.InvariantCulture) + ".html";
            _idBytes[i] = Encoding.UTF8.GetBytes(_ids[i]);
            _pageUrlBytes[i] = Encoding.UTF8.GetBytes(_pageUrls[i]);
        }

        _resolveSink = new(ResolveSinkCapacity);

        _populatedRegistry = new(EntryCount);
        for (var i = 0; i < EntryCount; i++)
        {
            var idSpan = (ReadOnlySpan<byte>)_idBytes[i];
            _populatedRegistry.Register(idSpan, _pageUrlBytes[i], idSpan);
        }
    }

    /// <summary>Resets the registry per iteration so cold-insert cost is measured.</summary>
    [IterationSetup]
    public void IterationSetup() => _registry = new();

    /// <summary>Registers every entry with <c>fragment == id</c> — the heading-scan path.</summary>
    /// <returns>Final entry count.</returns>
    [Benchmark(Baseline = true)]
    public int RegisterWithFragment()
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            _registry.Register(_ids[i], _pageUrls[i], _ids[i]);
        }

        return _registry.Count;
    }

    /// <summary>Registers every entry with no fragment — the whole-page path.</summary>
    /// <returns>Final entry count.</returns>
    [Benchmark]
    public int RegisterWithoutFragment()
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            _registry.Register(_ids[i], _pageUrls[i], fragment: null);
        }

        return _registry.Count;
    }

    /// <summary>Heading-scan path against a registry pre-sized to the expected entry count — should erase the resize cliff.</summary>
    /// <returns>Final entry count.</returns>
    [Benchmark]
    public int RegisterWithFragmentPreSized()
    {
        var registry = new AutorefsRegistry(_ids.Length);
        for (var i = 0; i < _ids.Length; i++)
        {
            registry.Register(_ids[i], _pageUrls[i], _ids[i]);
        }

        return registry.Count;
    }

    /// <summary>Heading-scan path through the byte-shaped Register overload.</summary>
    /// <returns>Final entry count.</returns>
    /// <remarks>Measures the actual rxui-corpus hot path with no per-call UTF-8 encoding.</remarks>
    [Benchmark]
    public int RegisterWithFragmentBytes()
    {
        for (var i = 0; i < _idBytes.Length; i++)
        {
            var idSpan = (ReadOnlySpan<byte>)_idBytes[i];
            _registry.Register(idSpan, _pageUrlBytes[i], idSpan);
        }

        return _registry.Count;
    }

    /// <summary>Resolves every registered id back through the byte-path resolve into a reused sink.</summary>
    /// <returns>Total bytes written across the resolve loop.</returns>
    /// <remarks>Mirrors the cost the autoref rewriter pays per <c>@autoref:ID</c> marker on a finalized page.</remarks>
    [Benchmark]
    public int ResolveIntoBytes()
    {
        var total = 0;
        for (var i = 0; i < _idBytes.Length; i++)
        {
            _resolveSink.ResetWrittenCount();
            _populatedRegistry.TryResolveInto(_idBytes[i], _resolveSink);
            total += _resolveSink.WrittenCount;
        }

        return total;
    }
}
