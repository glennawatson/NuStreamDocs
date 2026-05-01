// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Plugins;
using NuStreamDocs.Snippets;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <see cref="SnippetsPlugin"/>.</summary>
/// <remarks>
/// Three scenarios — whole-file include, section-include via the <c>file#section</c>
/// syntax, and a no-marker pass-through — pin the cost of the three paths real
/// pages take through the rewriter.
/// </remarks>
[MemoryDiagnoser]
public class SnippetsBenchmarks
{
    /// <summary>Per-fixture include count.</summary>
    private const int Repetitions = 100;

    /// <summary>Temp directory hosting the snippet files for the lifetime of the benchmark.</summary>
    private string _baseDir = string.Empty;

    /// <summary>Pre-built whole-file <c>--8&lt;-- "file"</c> source.</summary>
    private byte[] _wholeFileSource = [];

    /// <summary>Pre-built section <c>--8&lt;-- "file#name"</c> source.</summary>
    private byte[] _sectionSource = [];

    /// <summary>Pre-built no-marker source (plain prose).</summary>
    private byte[] _noMarkerSource = [];

    /// <summary>Configured plugin instance.</summary>
    private SnippetsPlugin _plugin = null!;

    /// <summary>Allocates the snippet fixtures + plugin.</summary>
    /// <returns>Task tracking the async configure call.</returns>
    [GlobalSetup]
    public async ValueTask SetupAsync()
    {
        _baseDir = Path.Combine(
            Path.GetTempPath(),
            "smkd-snip-bench-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_baseDir);
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "whole.md"), "Hello from the whole-file snippet.\n").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(_baseDir, "sectioned.md"),
            "Header.\n<!-- @section example -->\nSection body that gets spliced.\n<!-- @endsection -->\nFooter.\n").ConfigureAwait(false);

        _wholeFileSource = BuildRepeated("--8<-- \"whole.md\"\n");
        _sectionSource = BuildRepeated("--8<-- \"sectioned.md#example\"\n");
        _noMarkerSource = BuildRepeated("Plain markdown line with no include directive anywhere here.\n");

        _plugin = new SnippetsPlugin(_baseDir);

        // The plugin captures _baseDir lazily on OnConfigureAsync; force it now so the
        // benchmark only measures the per-page rewrite cost.
        var ctx = new NuStreamDocs.Plugins.PluginConfigureContext(default, _baseDir, _baseDir, []);
        await _plugin.OnConfigureAsync(ctx, default).ConfigureAwait(false);
    }

    /// <summary>Cleans up the snippet fixtures.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (string.IsNullOrEmpty(_baseDir) || !Directory.Exists(_baseDir))
        {
            return;
        }

        try
        {
            Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>Whole-file include — every directive splices the resolved file inline.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int WholeFileInclude()
    {
        var sink = new ArrayBufferWriter<byte>(_wholeFileSource.Length * 2);
        _plugin.Preprocess(_wholeFileSource, sink);
        return sink.WrittenCount;
    }

    /// <summary>Section include — every directive splices a single <c>&lt;!-- @section --&gt;</c> block.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int SectionInclude()
    {
        var sink = new ArrayBufferWriter<byte>(_sectionSource.Length * 2);
        _plugin.Preprocess(_sectionSource, sink);
        return sink.WrittenCount;
    }

    /// <summary>No-marker fixture — exercises the line-walk early-out path.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int NoMarkerPassThrough()
    {
        var sink = new ArrayBufferWriter<byte>(_noMarkerSource.Length * 2);
        _plugin.Preprocess(_noMarkerSource, sink);
        return sink.WrittenCount;
    }

    /// <summary>Stamps <paramref name="block"/> <see cref="Repetitions"/> times into a UTF-8 buffer.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>Pre-built fixture bytes.</returns>
    private static byte[] BuildRepeated(string block)
    {
        var blockBytes = Encoding.UTF8.GetBytes(block);
        var output = new byte[blockBytes.Length * Repetitions];
        for (var i = 0; i < Repetitions; i++)
        {
            blockBytes.AsSpan().CopyTo(output.AsSpan(i * blockBytes.Length));
        }

        return output;
    }
}
