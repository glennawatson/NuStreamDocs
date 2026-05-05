// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Abbr;
using NuStreamDocs.Plugins;
using NuStreamDocs.SmartSymbols;

namespace NuStreamDocs.Benchmarks;

/// <summary>Micro-benchmarks for inline markdown rewriters that operate on the byte-level fast path.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class InlineRewriterBenchmarks
{
    /// <summary>Number of times each benchmark block is repeated.</summary>
    private const int Repetitions = 120;

    /// <summary>Pre-built abbreviation fixture with multiple definitions and repeated usages.</summary>
    private byte[] _abbr = [];

    /// <summary>Pre-built smart-symbols fixture with multiple rewrite opportunities.</summary>
    private byte[] _smartSymbols = [];

    /// <summary>Reused abbreviation plugin instance.</summary>
    private AbbrPlugin _abbrPlugin = null!;

    /// <summary>Reused smart-symbols plugin instance.</summary>
    private SmartSymbolsPlugin _smartSymbolsPlugin = null!;

    /// <summary>Creates the benchmark fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _abbr = Encoding.UTF8.GetBytes(Repeat(
            "*[API]: Application Programming Interface\n"
            + "*[UI]: User Interface\n"
            + "*[RX]: Reactive Extensions\n"
            + "The API drives the UI while RX keeps the API and UI in sync.\n",
            Repetitions));
        _smartSymbols = Encoding.UTF8.GetBytes(Repeat(
            "(c) (tm) c/o 1/2 3/4 +/- =/= --> <-- <--> ==> <== <==>\n",
            Repetitions));
        _abbrPlugin = new();
        _smartSymbolsPlugin = new();
    }

    /// <summary>Benchmark: abbreviation rewrite.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Abbr()
    {
        ArrayBufferWriter<byte> sink = new(_abbr.Length * 2);
        PagePreRenderContext ctx = new("page.md", _abbr, sink);
        _abbrPlugin.PreRender(in ctx);
        return sink.WrittenCount;
    }

    /// <summary>Benchmark: smart-symbols rewrite.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int SmartSymbols()
    {
        ArrayBufferWriter<byte> sink = new(_smartSymbols.Length * 2);
        PagePreRenderContext ctx = new("page.md", _smartSymbols, sink);
        _smartSymbolsPlugin.PreRender(in ctx);
        return sink.WrittenCount;
    }

    /// <summary>Repeats <paramref name="block"/> <paramref name="count"/> times.</summary>
    /// <param name="block">Single source block.</param>
    /// <param name="count">Repetition count.</param>
    /// <returns>The concatenated string.</returns>
    private static string Repeat(string block, int count)
    {
        StringBuilder builder = new(block.Length * count);
        for (var i = 0; i < count; i++)
        {
            builder.Append(block);
        }

        return builder.ToString();
    }
}
