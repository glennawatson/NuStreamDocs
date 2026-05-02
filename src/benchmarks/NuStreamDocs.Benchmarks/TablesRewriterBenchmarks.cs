// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.MarkdownExtensions.Tables;

namespace NuStreamDocs.Benchmarks;

/// <summary>Direct micro-benchmarks for <c>TablesRewriter.Rewrite</c>; isolates the parse + emit hot path from the plugin context wrapper.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class TablesRewriterBenchmarks
{
    /// <summary>Tables stamped per fixture.</summary>
    private const int TableCount = 100;

    /// <summary>Headroom factor for the output writer (HTML is ~6× the markdown for typical 3×3 tables).</summary>
    private const int OutputExpansionFactor = 8;

    /// <summary>Column count for the wide fixture.</summary>
    private const int WideColumnCount = 8;

    /// <summary>Body-row count for the tall fixture.</summary>
    private const int TallRowCount = 20;

    /// <summary>Small 3×3 fixture (3 columns, 2 body rows) — the most common shape.</summary>
    private byte[] _small = [];

    /// <summary>Wide 8×3 fixture — exercises the per-cell loop with realistic column counts.</summary>
    private byte[] _wide = [];

    /// <summary>Tall 3×20 fixture — many body rows with the same separator.</summary>
    private byte[] _tall = [];

    /// <summary>Reused output writer; pre-sized once in <c>Setup</c> and reset per iteration to mirror the per-thread cache pattern in production callers.</summary>
    private ArrayBufferWriter<byte> _writer = null!;

    /// <summary>Generates the input fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _small = Repeat("| h1 | h2 | h3 |\n| :--- | :---: | ---: |\n| a | b | c |\n| d | e | f |\n\n", TableCount);
        _wide = Repeat(BuildWide(WideColumnCount), TableCount);
        _tall = Repeat(BuildTall(TallRowCount), TableCount);

        var maxLen = Math.Max(Math.Max(_small.Length, _wide.Length), _tall.Length);
        _writer = new(maxLen * OutputExpansionFactor);
    }

    /// <summary>Direct rewrite of the small 3×3 fixture.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int RewriteSmall()
    {
        _writer.ResetWrittenCount();
        TablesRewriter.Rewrite(_small, _writer);
        return _writer.WrittenCount;
    }

    /// <summary>Direct rewrite of the 8-column fixture.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int RewriteWide()
    {
        _writer.ResetWrittenCount();
        TablesRewriter.Rewrite(_wide, _writer);
        return _writer.WrittenCount;
    }

    /// <summary>Direct rewrite of the 20-row fixture.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int RewriteTall()
    {
        _writer.ResetWrittenCount();
        TablesRewriter.Rewrite(_tall, _writer);
        return _writer.WrittenCount;
    }

    /// <summary>Builds an N-column header + 2 body rows.</summary>
    /// <param name="columns">Column count.</param>
    /// <returns>Markdown source.</returns>
    private static string BuildWide(int columns)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < columns; i++)
        {
            sb.Append("| h").Append(i).Append(' ');
        }

        sb.Append("|\n");
        for (var i = 0; i < columns; i++)
        {
            sb.Append("| --- ");
        }

        sb.Append("|\n");
        for (var i = 0; i < columns; i++)
        {
            sb.Append("| a").Append(i).Append(' ');
        }

        sb.Append("|\n");
        for (var i = 0; i < columns; i++)
        {
            sb.Append("| b").Append(i).Append(' ');
        }

        sb.Append("|\n\n");
        return sb.ToString();
    }

    /// <summary>Builds a 3-column header + N body rows.</summary>
    /// <param name="rows">Body-row count.</param>
    /// <returns>Markdown source.</returns>
    private static string BuildTall(int rows)
    {
        var sb = new StringBuilder("| h1 | h2 | h3 |\n| --- | --- | --- |\n");
        for (var i = 0; i < rows; i++)
        {
            sb.Append("| r").Append(i).Append("a | r").Append(i).Append("b | r").Append(i).Append("c |\n");
        }

        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Repeats <paramref name="block"/> <paramref name="count"/> times and encodes as UTF-8.</summary>
    /// <param name="block">Source fragment.</param>
    /// <param name="count">Repetition count.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] Repeat(string block, int count)
    {
        var sb = new StringBuilder(block.Length * count);
        for (var i = 0; i < count; i++)
        {
            sb.Append(block);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
