// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Snippets;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <see cref="SnippetsPlugin"/>.</summary>
/// <remarks>
/// Three scenarios — whole-file include, section-include via the <c>file#section</c>
/// syntax, and a no-marker pass-through — pin the cost of the three paths real
/// pages take through the rewriter.
/// </remarks>
[DebuggerDisplay("SnippetsBenchmarks: baseDir={_baseDir}, wholeFileSource={_wholeFileSource}")]
[ShortRunJob]
[MemoryDiagnoser]
public class SnippetsBenchmarks
{
    /// <summary>Per-fixture include count.</summary>
    private const int Repetitions = 100;

    /// <summary>Headroom for expanded snippet content.</summary>
    private const int OutputExpansionFactor = 2;

    /// <summary>Temp directory hosting the snippet files for the lifetime of the benchmark.</summary>
    private DirectoryPath _baseDir;

    /// <summary>Pre-built whole-file <c>--8&lt;-- "file"</c> source.</summary>
    private byte[] _wholeFileSource = [];

    /// <summary>Pre-built section <c>--8&lt;-- "file#name"</c> source.</summary>
    private byte[] _sectionSource = [];

    /// <summary>Pre-built no-marker source (plain prose).</summary>
    private byte[] _noMarkerSource = [];

    /// <summary>Configured plugin instance.</summary>
    private SnippetsPlugin _plugin = null!;

    /// <summary>Gets the page path used for snippet resolution.</summary>
    private static FilePath PagePath => "page.md";

    /// <summary>Allocates the snippet fixtures + plugin.</summary>
    /// <returns>Task tracking the async configure call.</returns>
    [GlobalSetup]
    public async ValueTask SetupAsync()
    {
        _baseDir = Path.Combine(
            Path.GetTempPath(),
            StringCompose.Concat("smkd-snip-bench-", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
        _ = Directory.CreateDirectory(_baseDir);
        await File.WriteAllTextAsync(Path.Combine(_baseDir, "whole.md"), "Hello from the whole-file snippet.\n")
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(_baseDir, "sectioned.md"),
                "Header.\n<!-- @section example -->\nSection body that gets spliced.\n<!-- @endsection -->\nFooter.\n")
            .ConfigureAwait(false);

        _wholeFileSource = BuildRepeated("--8<-- \"whole.md\"\n"u8);
        _sectionSource = BuildRepeated("--8<-- \"sectioned.md#example\"\n"u8);
        _noMarkerSource = BuildRepeated("Plain markdown line with no include directive anywhere here.\n"u8);

        _plugin = new(_baseDir);

        // The plugin captures _baseDir lazily on ConfigureAsync; force it now so the
        // benchmark only measures the per-page rewrite cost.
        BuildConfigureContext ctx = new(_baseDir, _baseDir, [_plugin], new());
        await _plugin.ConfigureAsync(ctx, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Cleans up the snippet fixtures.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (_baseDir.IsEmpty || !Directory.Exists(_baseDir))
        {
            return;
        }

        try
        {
            Directory.Delete(_baseDir, true);
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
        using var rental = PageBuilderPool.Rent(_wholeFileSource.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new(PagePath, _wholeFileSource, rental.Writer);
        _plugin.PreRender(in ctx);
        return rental.Writer.WrittenCount;
    }

    /// <summary>Section include — every directive splices a single <c>&lt;!-- @section --&gt;</c> block.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int SectionInclude()
    {
        using var rental = PageBuilderPool.Rent(_sectionSource.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new(PagePath, _sectionSource, rental.Writer);
        _plugin.PreRender(in ctx);
        return rental.Writer.WrittenCount;
    }

    /// <summary>No-marker fixture — exercises the line-walk early-out path.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int NoMarkerPassThrough()
    {
        using var rental = PageBuilderPool.Rent(_noMarkerSource.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new(PagePath, _noMarkerSource, rental.Writer);
        _plugin.PreRender(in ctx);
        return rental.Writer.WrittenCount;
    }

    /// <summary>Stamps <paramref name="block"/> <see cref="Repetitions"/> times into a UTF-8 buffer.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>Pre-built fixture bytes.</returns>
    private static byte[] BuildRepeated(ReadOnlySpan<byte> block)
    {
        var output = new byte[block.Length * Repetitions];
        for (var i = 0; i < Repetitions; i++)
        {
            block.CopyTo(output.AsSpan(i * block.Length));
        }

        return output;
    }
}
