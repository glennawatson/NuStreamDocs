// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Autorefs;
using NuStreamDocs.Plugins;
using NuStreamDocs.SphinxInventory;

namespace NuStreamDocs.Benchmarks;

/// <summary>End-of-build cost benchmarks for <see cref="SphinxInventoryPlugin"/>.</summary>
/// <remarks>
/// Sphinx inventory emit runs once per build, so absolute throughput is less
/// interesting than the per-entry encode + zlib cost — the benchmark fans out
/// across registry sizes that bracket realistic doc sites.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class SphinxInventoryBenchmarks
{
    /// <summary>Small-doc-site registry size — matches a typical project's API surface.</summary>
    private const int SmallSite = 100;

    /// <summary>Mid-sized doc-site registry — comparable to a multi-package library.</summary>
    private const int MidSite = 1_000;

    /// <summary>Large doc-site registry — exercises the registry snapshot + zlib compression cost.</summary>
    private const int LargeSite = 10_000;

    /// <summary>Per-iteration output directory — re-created so each emit writes a fresh file.</summary>
    private string _outputDir = string.Empty;

    /// <summary>Configured plugin instance — backed by the registry sized to <see cref="EntryCount"/>.</summary>
    private SphinxInventoryPlugin _plugin = null!;

    /// <summary>Gets or sets the number of <c>(uid, href)</c> entries to register for each iteration.</summary>
    [Params(SmallSite, MidSite, LargeSite)]
    public int EntryCount { get; set; }

    /// <summary>Allocates the registry + plugin once for the param set.</summary>
    [GlobalSetup]
    public void Setup()
    {
        AutorefsRegistry registry = new(EntryCount);
        for (var i = 0; i < EntryCount; i++)
        {
            var idx = i.ToString(CultureInfo.InvariantCulture);
            var idBytes = Encoding.UTF8.GetBytes("Symbol_" + idx);
            var urlBytes = Encoding.UTF8.GetBytes("api/Symbol_" + idx + ".html");
            registry.Register(idBytes, urlBytes, fragment: default);
        }

        _plugin = new(registry);
    }

    /// <summary>Allocates the per-iteration output directory.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        _outputDir = Path.Combine(
            Path.GetTempPath(),
            "smkd-inv-bench-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_outputDir);
    }

    /// <summary>Cleans up the per-iteration output directory.</summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        if (string.IsNullOrEmpty(_outputDir) || !Directory.Exists(_outputDir))
        {
            return;
        }

        try
        {
            Directory.Delete(_outputDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; benchmarks fail-soft on tear-down.
        }
    }

    /// <summary>End-to-end finalize — encodes the registry, deflate-compresses the body, writes <c>objects.inv</c>.</summary>
    /// <returns>Bytes written to disk.</returns>
    [Benchmark]
    public async ValueTask<long> FinalizeEmit()
    {
        BuildFinalizeContext ctx = new(_outputDir, [_plugin]);
        await _plugin.FinalizeAsync(ctx, CancellationToken.None).ConfigureAwait(false);
        return new FileInfo(Path.Combine(_outputDir, "objects.inv")).Length;
    }
}
