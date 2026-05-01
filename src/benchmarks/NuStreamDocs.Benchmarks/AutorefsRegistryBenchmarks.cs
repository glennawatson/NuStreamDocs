// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Autorefs;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-call cost of <see cref="AutorefsRegistry.Register"/> across the
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

    /// <summary>Page URL pre-allocated once and reused for every call to remove encode-once-per-call noise.</summary>
    private string[] _pageUrls = [];

    /// <summary>IDs pre-allocated once and reused across iterations.</summary>
    private string[] _ids = [];

    /// <summary>Registry recreated per iteration so dictionary growth is measured.</summary>
    private AutorefsRegistry _registry = null!;

    /// <summary>Gets or sets the number of registrations per iteration.</summary>
    [Params(SmallSite, MidSite, LargeSite)]
    public int EntryCount { get; set; }

    /// <summary>Pre-allocates id + URL strings shared across iterations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _ids = new string[EntryCount];
        _pageUrls = new string[EntryCount];
        for (var i = 0; i < EntryCount; i++)
        {
            var idx = i.ToString(CultureInfo.InvariantCulture);
            _ids[i] = "section-" + idx;

            _pageUrls[i] = "guide/page-" + (i / HeadingsPerPage).ToString(CultureInfo.InvariantCulture) + ".html";
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
}
