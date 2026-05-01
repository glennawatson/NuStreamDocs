// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
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

/// <summary>Throughput + allocation benchmarks for every <c>IMarkdownPreprocessor</c> shipped in <c>NuStreamDocs.MarkdownExtensions</c>.</summary>
[MemoryDiagnoser]
public class MarkdownExtensionsBenchmarks
{
    /// <summary>Number of repeated extension blocks to stamp into each fixture document.</summary>
    private const int Repetitions = 100;

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
        _admonitionSource = BuildRepeated("!!! note \"Heads up\"\n    body line one\n    body line two\n\n");
        _detailsSource = BuildRepeated("???+ tip \"Try this\"\n    body line one\n    body line two\n\n");
        _tabsSource = BuildRepeated("=== \"First\"\n    one\n=== \"Second\"\n    two\n\n");
        _checkListSource = BuildRepeated("- [x] done\n- [ ] todo\n");
        _markSource = BuildRepeated("This text has ==important== highlighted markers everywhere.\n");
        _defListSource = BuildRepeated("Term\n: first definition\n: second definition\n\n");
        _footnotesSource = BuildRepeated("Body[^1] with a reference.\n\n[^1]: definition with **bold**.\n\n");
        _tablesSource = BuildRepeated("| h1 | h2 | h3 |\n| :--- | :---: | ---: |\n| a | b | c |\n| d | e | f |\n\n");
    }

    /// <summary>Benchmark for <c>AdmonitionPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Admonitions() => Run(new AdmonitionPlugin(), _admonitionSource);

    /// <summary>Benchmark for <c>DetailsPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Details() => Run(new DetailsPlugin(), _detailsSource);

    /// <summary>Benchmark for <c>TabsPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Tabs() => Run(new TabsPlugin(), _tabsSource);

    /// <summary>Benchmark for <c>CheckListPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int CheckList() => Run(new CheckListPlugin(), _checkListSource);

    /// <summary>Benchmark for <c>MarkPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Mark() => Run(new MarkPlugin(), _markSource);

    /// <summary>Benchmark for <c>DefListPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int DefList() => Run(new DefListPlugin(), _defListSource);

    /// <summary>Benchmark for <c>FootnotesPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Footnotes() => Run(new FootnotesPlugin(), _footnotesSource);

    /// <summary>Benchmark for <c>TablesPlugin</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Tables() => Run(new TablesPlugin(), _tablesSource);

    /// <summary>Builds a UTF-8 fixture by stamping <paramref name="block"/> <c>Repetitions</c> times.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>UTF-8 bytes of the repeated fragment.</returns>
    private static byte[] BuildRepeated(string block)
    {
        var sb = new StringBuilder(block.Length * Repetitions);
        for (var i = 0; i < Repetitions; i++)
        {
            sb.Append(block);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Runs <paramref name="plugin"/>'s <c>IMarkdownPreprocessor.Preprocess</c> against <paramref name="source"/>.</summary>
    /// <param name="plugin">Preprocessor under test.</param>
    /// <param name="source">UTF-8 input bytes.</param>
    /// <returns>Bytes written to the sink.</returns>
    private static int Run(IMarkdownPreprocessor plugin, byte[] source)
    {
        var sink = new ArrayBufferWriter<byte>(source.Length * 2);
        plugin.Preprocess(source, sink);
        return sink.WrittenCount;
    }
}
