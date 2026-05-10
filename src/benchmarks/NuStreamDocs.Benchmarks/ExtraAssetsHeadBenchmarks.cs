// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;
using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Configure-time head-fragment composition + per-page <c>WriteHeadExtra</c> for
/// <see cref="ExtraAssetsPlugin"/>. The new <c>type="module"</c> branch is exercised
/// alongside the legacy <c>defer</c> path so any regression in either is caught.
/// </summary>
/// <remarks>
/// <c>Configure</c> runs once per build; <c>WriteHeadExtra</c> runs once per page. We benchmark
/// both because the per-build composition is the only place where the new module branch costs
/// anything; per-page emission is a single byte-copy regardless.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class ExtraAssetsHeadBenchmarks
{
    /// <summary>Total scripts registered — split evenly between module and non-module.</summary>
    private const int ScriptCount = 8;

    /// <summary>Stylesheets registered.</summary>
    private const int CssCount = 3;

    /// <summary>Modulo cycle that interleaves module / non-module scripts.</summary>
    private const int ModuleStridePeriod = 2;

    /// <summary>Initial sink capacity — head fragment for 8 scripts + 3 stylesheets is well under 2 KB.</summary>
    private const int SinkCapacity = 2048;

    /// <summary>Pre-configured plugin instance used by the per-page benchmark.</summary>
    private ExtraAssetsPlugin _plugin = null!;

    /// <summary>Per-instance scratch dir holding the mock asset files.</summary>
    private string _tempRoot = string.Empty;

    /// <summary>Reused sink so per-iteration allocation reflects the head writer, not the buffer.</summary>
    private ArrayBufferWriter<byte> _sink = null!;

    /// <summary>Configure-context handed to both the steady-state plugin and the from-scratch reconfigures.</summary>
    private BuildConfigureContext _ctx;

    /// <summary>Builds the plugin, writes mock asset files, and runs the configure pass once so per-page WriteHeadExtra is the only thing measured.</summary>
    /// <returns>Async setup task.</returns>
    [GlobalSetup]
    public async ValueTask SetupAsync()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-extra-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_tempRoot);

        var jsBytes = "console.log('hello');"u8.ToArray();
        var cssBytes = ".foo { color: red; }"u8.ToArray();

        DocBuilder builder = new();
        for (var i = 0; i < CssCount; i++)
        {
            var css = Path.Combine(_tempRoot, "style-" + i + ".css");
            await File.WriteAllBytesAsync(css, cssBytes).ConfigureAwait(false);
            builder.AddExtraCss((Common.FilePath)css);
        }

        for (var i = 0; i < ScriptCount; i++)
        {
            var js = Path.Combine(_tempRoot, "script-" + i + ".js");
            await File.WriteAllBytesAsync(js, jsBytes).ConfigureAwait(false);
            if (i % ModuleStridePeriod == 0)
            {
                builder.AddExtraJs((Common.FilePath)js);
            }
            else
            {
                builder.AddExtraJsModule((Common.FilePath)js);
            }
        }

        _plugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        _ctx = new BuildConfigureContext(_tempRoot, _tempRoot, [], new());
        await _plugin.ConfigureAsync(_ctx, default).ConfigureAwait(false);
        _sink = new(SinkCapacity);
    }

    /// <summary>Tears down the temp dir.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>Per-page <c>WriteHeadExtra</c> — should be a single buffer copy of the pre-composed fragment.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark(Baseline = true)]
    public int WriteHeadExtra()
    {
        _sink.ResetWrittenCount();
        _plugin.WriteHeadExtra(_sink);
        return _sink.WrittenCount;
    }

    /// <summary>One-shot configure pass — composes the head fragment + reads asset bytes from disk.</summary>
    /// <remarks>Iterates a fresh plugin per benchmark so the configure path is actually re-run; the steady-state plugin in <see cref="WriteHeadExtra"/> is constructed once.</remarks>
    /// <returns>Length of the freshly-composed head fragment.</returns>
    [Benchmark]
    public async ValueTask<int> ConfigureFromScratch()
    {
        DocBuilder builder = new();
        for (var i = 0; i < CssCount; i++)
        {
            builder.AddExtraCss((Common.FilePath)Path.Combine(_tempRoot, "style-" + i + ".css"));
        }

        for (var i = 0; i < ScriptCount; i++)
        {
            var js = Path.Combine(_tempRoot, "script-" + i + ".js");
            if (i % ModuleStridePeriod == 0)
            {
                builder.AddExtraJs((Common.FilePath)js);
            }
            else
            {
                builder.AddExtraJsModule((Common.FilePath)js);
            }
        }

        var freshPlugin = builder.GetOrAddPlugin<ExtraAssetsPlugin>();
        await freshPlugin.ConfigureAsync(_ctx, default).ConfigureAwait(false);
        ArrayBufferWriter<byte> probe = new(SinkCapacity);
        freshPlugin.WriteHeadExtra(probe);
        return probe.WrittenCount;
    }
}
