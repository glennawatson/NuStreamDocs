// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Nav;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>Focused benchmarks for the nav plugin's per-page render path.</summary>
/// <remarks>
/// Builds one synthetic docs tree on disk, discovers full + pruned
/// <see cref="NavPlugin"/> instances once, then repeatedly measures
/// <see cref="NavPlugin.PostRender(in PagePostRenderContext)"/>
/// against a themed page containing only the nav marker.
/// <para>
/// The corpus is deliberately section-heavy so we stress the active-path
/// lookup, URL emission, and branch-pruning work without dragging the rest
/// of the build pipeline into the measurement.
/// </para>
/// </remarks>
[DebuggerDisplay("NavRenderBenchmarks: inputRoot={_inputRoot}, outputRoot={_outputRoot}")]
[ShortRunJob]
[MemoryDiagnoser]
public class NavRenderBenchmarks
{
    /// <summary>Number of top-level sections in the synthetic corpus.</summary>
    private const int TopLevelSections = 48;

    /// <summary>Leaf pages written directly under each top-level section.</summary>
    private const int SectionLeafPages = 24;

    /// <summary>Leaf pages written under each nested section.</summary>
    private const int NestedLeafPages = 12;

    /// <summary>Initial buffer capacity for the full-nav render benchmark.</summary>
    private const int FullHtmlCapacity = 512 * 1024;

    /// <summary>Initial buffer capacity for the pruned-nav render benchmark.</summary>
    private const int PrunedHtmlCapacity = 64 * 1024;

    /// <summary>Source-relative path of the page used as the active branch during render.</summary>
    private const string ActivePage = "section-47/deep/page-11.md";

    /// <summary>Temp input root containing the synthetic docs tree.</summary>
    private DirectoryPath _inputRoot;

    /// <summary>Temp output root passed to the plugin configure context.</summary>
    private DirectoryPath _outputRoot;

    /// <summary>Plugin configured for full-tree rendering.</summary>
    private NavPlugin _fullPlugin = null!;

    /// <summary>Plugin configured for pruned rendering.</summary>
    private NavPlugin _prunedPlugin = null!;

    /// <summary>Writer reused by the full render benchmark.</summary>
    private ArrayBufferWriter<byte> _fullHtml = null!;

    /// <summary>Writer reused by the pruned render benchmark.</summary>
    private ArrayBufferWriter<byte> _prunedHtml = null!;

    /// <summary>Gets the root index page body.</summary>
    private static ReadOnlySpan<byte> RootIndexPage => "# Docs\n\nSynthetic nav benchmark corpus.\n"u8;

    /// <summary>Gets the index filename used by each section.</summary>
    private static FilePath IndexFile => new("index.md");

    /// <summary>Builds the synthetic corpus and configures both nav-plugin variants.</summary>
    /// <returns>A task representing the asynchronous setup.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _inputRoot = Path.Combine(
            Path.GetTempPath(),
            StringCompose.Concat("smkd-nav-bench-in-", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
        _outputRoot = Path.Combine(
            Path.GetTempPath(),
            StringCompose.Concat("smkd-nav-bench-out-", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)));
        _ = Directory.CreateDirectory(_inputRoot);
        _ = Directory.CreateDirectory(_outputRoot);

        CreateCorpus(_inputRoot);

        _fullHtml = new(FullHtmlCapacity);
        _prunedHtml = new(PrunedHtmlCapacity);
        _fullPlugin = new(NavOptions.Default with { WarnOnOrphanPages = false });
        _prunedPlugin = new(NavOptions.Default with { Prune = true, WarnOnOrphanPages = false });

        await ConfigureAsync(_fullPlugin, _inputRoot, _outputRoot).ConfigureAwait(false);
        await ConfigureAsync(_prunedPlugin, _inputRoot, _outputRoot).ConfigureAwait(false);
    }

    /// <summary>Deletes the temp corpus after the benchmark run completes.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        TryDelete(_inputRoot);
        TryDelete(_outputRoot);
    }

    /// <summary>Measures the full-nav render path for a deep active page.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark(Baseline = true)]
    public int RenderFull() =>
        Render(_fullPlugin, _fullHtml);

    /// <summary>Measures the pruned-nav render path for the same deep active page.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int RenderPruned() =>
        Render(_prunedPlugin, _prunedHtml);

    /// <summary>Creates the synthetic docs tree used by the benchmark.</summary>
    /// <param name="inputRoot">Absolute input root.</param>
    private static void CreateCorpus(DirectoryPath inputRoot)
    {
        inputRoot.File(IndexFile.Value).WriteAllBytes(RootIndexPage);

        for (var section = 0; section < TopLevelSections; section++)
        {
            var sectionName = new PathSegment(StringCompose.Concat("section-", section.ToString("D2", CultureInfo.InvariantCulture)));
            var sectionDir = inputRoot / sectionName.Value;
            var nestedDir = sectionDir / "deep";
            _ = Directory.CreateDirectory(nestedDir);

            File.WriteAllText(sectionDir.File(IndexFile.Value), SectionIndexPage(sectionName));
            File.WriteAllText(nestedDir.File(IndexFile.Value), NestedIndexPage(sectionName));

            for (var page = 0; page < SectionLeafPages; page++)
            {
                File.WriteAllText(
                    sectionDir.File(StringCompose.Concat("page-", page.ToString("D2", CultureInfo.InvariantCulture), ".md")),
                    Page(sectionName, page, false));
            }

            for (var page = 0; page < NestedLeafPages; page++)
            {
                File.WriteAllText(
                    nestedDir.File(StringCompose.Concat("page-", page.ToString("D2", CultureInfo.InvariantCulture), ".md")),
                    Page(sectionName, page, true));
            }
        }
    }

    /// <summary>Runs one nav render into <paramref name="html"/>.</summary>
    /// <param name="plugin">Configured plugin.</param>
    /// <param name="html">Writer reused across invocations.</param>
    /// <returns>Bytes written.</returns>
    private static int Render(NavPlugin plugin, ArrayBufferWriter<byte> html)
    {
        html.ResetWrittenCount();
        PagePostRenderContext context = new(ActivePage, default, "<nav><!--@@nav@@--></nav>"u8, html);
        plugin.PostRender(in context);
        return html.WrittenCount;
    }

    /// <summary>Discovers one plugin instance against the synthetic corpus.</summary>
    /// <param name="plugin">Plugin to discover.</param>
    /// <param name="inputRoot">Absolute input root.</param>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <returns>A task representing the asynchronous setup.</returns>
    private static Task ConfigureAsync(NavPlugin plugin, DirectoryPath inputRoot, DirectoryPath outputRoot)
    {
        BuildDiscoverContext context = new(inputRoot, outputRoot, [plugin], new());
        return plugin.DiscoverAsync(context, CancellationToken.None).AsTask();
    }

    /// <summary>Best-effort recursive directory delete.</summary>
    /// <param name="path">Directory path.</param>
    private static void TryDelete(DirectoryPath path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>Builds one section index page.</summary>
    /// <param name="sectionName">Section folder name.</param>
    /// <returns>Markdown body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ApiCompatString SectionIndexPage(PathSegment sectionName) =>
        StringCompose.Concat("# ", sectionName.Value, "\n\nSection landing page.\n");

    /// <summary>Builds one nested-section index page.</summary>
    /// <param name="sectionName">Top-level section folder name.</param>
    /// <returns>Markdown body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ApiCompatString NestedIndexPage(PathSegment sectionName) =>
        StringCompose.Concat("# ", sectionName.Value, " deep\n\nNested landing page.\n");

    /// <summary>Builds one leaf page body.</summary>
    /// <param name="sectionName">Owning section folder name.</param>
    /// <param name="pageIndex">Page index within the section.</param>
    /// <param name="nested">True when the page lives under the nested section.</param>
    /// <returns>Markdown body.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ApiCompatString Page(PathSegment sectionName, int pageIndex, bool nested) =>
        StringCompose.Concat(
            "# ",
            sectionName.Value,
            " page ",
            pageIndex.ToString(CultureInfo.InvariantCulture),
            nested ? "\n\nNested page for nav-render benchmarks.\n" : "\n\nSection page for nav-render benchmarks.\n");
}
