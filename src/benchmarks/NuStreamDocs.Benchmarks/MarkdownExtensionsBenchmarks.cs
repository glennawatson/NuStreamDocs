// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.MarkdownExtensions.Admonitions;
using NuStreamDocs.MarkdownExtensions.CheckList;
using NuStreamDocs.MarkdownExtensions.DefList;
using NuStreamDocs.MarkdownExtensions.Details;
using NuStreamDocs.MarkdownExtensions.Footnotes;
using NuStreamDocs.MarkdownExtensions.Mark;
using NuStreamDocs.MarkdownExtensions.Tables;
using NuStreamDocs.MarkdownExtensions.Tabs;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for every <c>IPagePreRenderPlugin</c> shipped in <c>NuStreamDocs.MarkdownExtensions</c>.</summary>
[DebuggerDisplay("MarkdownExtensionsBenchmarks: admonitionSource={_admonitionSource}, detailsSource={_detailsSource}")]
[ShortRunJob]
[MemoryDiagnoser]
public class MarkdownExtensionsBenchmarks
{
    /// <summary>Number of repeated extension blocks to stamp into each fixture document.</summary>
    private const int Repetitions = 100;

    /// <summary>Reserves room for rendered extension markup.</summary>
    private const int OutputExpansionFactor = 2;

    /// <summary>Pre-built admonition input.</summary>
    private byte[] _admonitionSource = [];

    /// <summary>Pre-built details input.</summary>
    private byte[] _detailsSource = [];

    /// <summary>Pre-built tabs input.</summary>
    private byte[] _tabsSource = [];

    /// <summary>Pre-built check-list input.</summary>
    private byte[] _checkListSource = [];

    /// <summary>Pre-built mark input.</summary>
    private byte[] _markSource = [];

    /// <summary>Pre-built deflist input.</summary>
    private byte[] _defListSource = [];

    /// <summary>Pre-built footnotes input.</summary>
    private byte[] _footnotesSource = [];

    /// <summary>Pre-built tables input.</summary>
    private byte[] _tablesSource = [];

    /// <summary>Generates the per-plugin input fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _admonitionSource = BuildRepeated("!!! note \"Heads up\"\n    body line one\n    body line two\n\n"u8);
        _detailsSource = BuildRepeated("???+ tip \"Try this\"\n    body line one\n    body line two\n\n"u8);
        _tabsSource = BuildRepeated("=== \"First\"\n    one\n=== \"Second\"\n    two\n\n"u8);
        _checkListSource = BuildRepeated("- [x] done\n- [ ] todo\n"u8);
        _markSource = BuildRepeated("This text has ==important== highlighted markers everywhere.\n"u8);
        _defListSource = BuildRepeated("Term\n: first definition\n: second definition\n\n"u8);
        _footnotesSource = BuildRepeated("Body[^1] with a reference.\n\n[^1]: definition with **bold**.\n\n"u8);
        _tablesSource = BuildRepeated("| h1 | h2 | h3 |\n| :--- | :---: | ---: |\n| a | b | c |\n| d | e | f |\n\n"u8);
    }

    /// <summary>Benchmark for <c>AdmonitionPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int Admonitions() => Run(new AdmonitionPlugin(), _admonitionSource);

    /// <summary>Benchmark for <c>DetailsPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int Details() => Run(new DetailsPlugin(), _detailsSource);

    /// <summary>Benchmark for <c>TabsPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int Tabs() => Run(new TabsPlugin(), _tabsSource);

    /// <summary>Benchmark for <c>CheckListPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int CheckList() => Run(new CheckListPlugin(), _checkListSource);

    /// <summary>Benchmark for <c>MarkPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int Mark() => Run(new MarkPlugin(), _markSource);

    /// <summary>Benchmark for <c>DefListPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int DefList() => Run(new DefListPlugin(), _defListSource);

    /// <summary>Benchmark for <c>FootnotesPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int Footnotes() => Run(new FootnotesPlugin(), _footnotesSource);

    /// <summary>Benchmark for <c>TablesPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Benchmark]
    public int Tables() => Run(new TablesPlugin(), _tablesSource);

    /// <summary>Builds a UTF-8 fixture by stamping <paramref name="block"/> <c>Repetitions</c> times.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>UTF-8 bytes of the repeated fragment.</returns>
    private static byte[] BuildRepeated(ReadOnlySpan<byte> block)
    {
        var output = new byte[block.Length * Repetitions];
        for (var i = 0; i < Repetitions; i++)
        {
            block.CopyTo(output.AsSpan(i * block.Length));
        }

        return output;
    }

    /// <summary>Runs <paramref name="plugin"/>'s <c>IPagePreRenderPlugin.PreRender</c> against <paramref name="source"/>.</summary>
    /// <param name="plugin">Pre-render plugin under test.</param>
    /// <param name="source">UTF-8 input bytes.</param>
    /// <returns>Bytes written to the sink.</returns>
    private static int Run(IPagePreRenderPlugin plugin, byte[] source)
    {
        ArrayBufferWriter<byte> sink = new(source.Length * OutputExpansionFactor);
        PagePreRenderContext ctx = new("page.md", source, sink);
        plugin.PreRender(in ctx);
        return sink.WrittenCount;
    }
}
