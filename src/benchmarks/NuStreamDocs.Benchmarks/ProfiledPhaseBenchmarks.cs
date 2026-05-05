// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using NuStreamDocs.Building;
using NuStreamDocs.Highlight;
using NuStreamDocs.Highlight.Languages;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Hotspot-profile benchmark bundle. Adds <c>EventPipeProfilerAttribute</c>
/// with <c>EventPipeProfile.GcVerbose</c> so each method emits a
/// <c>.nettrace</c> next to its result. Feed the trace to the
/// <c>smkd-allocreport</c> tool under <c>src/tools/NuStreamDocs.AllocReport</c>
/// to get a markdown table of the top-N types and call stacks by sampled bytes.
/// </summary>
/// <remarks>
/// Kept separate from the other benchmark classes so the profiler attaches
/// only here — running everything with the profiler turned on dilutes the
/// signal and slows the rest of the suite.
/// <para>
/// Each method holds onto a synthetic input that's representative of one
/// pipeline phase. The profiler captures every allocation tick during the
/// timed body, so the resulting trace is the place to look when optimizing
/// per-page allocations.
/// </para>
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
[SuppressMessage(
    "Major Code Smell",
    "S4462:Calls to \"async\" methods should not be blocking",
    Justification = "BenchmarkDotNet drives benchmarks synchronously; GetResult is the pragmatic way to measure end-to-end async pipelines.")]
public class ProfiledPhaseBenchmarks
{
    /// <summary>Number of pages in the on-disk corpus the build phase walks.</summary>
    private const int CorpusPages = 200;

    /// <summary>Number of paragraphs in the synthetic markdown payload used for parse/render phases.</summary>
    private const int Paragraphs = 500;

    /// <summary>Repetitions used to grow the lexer fixture.</summary>
    private const int LexerRepetitions = 80;

    /// <summary>Pre-built UTF-8 markdown fixture for parse + render phases.</summary>
    private byte[] _markdown = [];

    /// <summary>Pre-built C# fixture for the lexer phase.</summary>
    private byte[] _csharp = [];

    /// <summary>Absolute path to the corpus input root used by <c>EndToEndBuild</c>.</summary>
    private string _inputRoot = string.Empty;

    /// <summary>Absolute path to a fresh per-iteration output directory used by <c>EndToEndBuild</c>.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>Builds the synthetic inputs once.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        StringBuilder sb = new();
        for (var i = 0; i < Paragraphs; i++)
        {
            sb.Append("# Heading ").Append(i).Append('\n')
              .Append("Paragraph with **bold**, `code`, [link](https://x/").Append(i).Append(") and a > quote.\n\n")
              .Append("- list item ").Append(i).Append('\n')
              .Append("- list item B\n\n");
        }

        _markdown = Encoding.UTF8.GetBytes(sb.ToString());
        _csharp = RepeatBytes("public sealed class Foo { public int Bar(int x) => x + 1; /* comment */ string s = \"hi\"; }\n"u8, LexerRepetitions);

        _inputRoot = Path.Combine(Path.GetTempPath(), "smkd-prof-in-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        _outputRoot = Path.Combine(Path.GetTempPath(), "smkd-prof-out-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_inputRoot);
        for (var i = 0; i < CorpusPages; i++)
        {
            File.WriteAllText(Path.Combine(_inputRoot, $"page-{i}.md"), Page(i));
        }
    }

    /// <summary>Cleans the on-disk corpus once at the end.</summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        TryDelete(_inputRoot);
        TryDelete(_outputRoot);
    }

    /// <summary>Resets the per-iteration output directory.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        TryDelete(_outputRoot);
        Directory.CreateDirectory(_outputRoot);
    }

    /// <summary>Block-scan only.</summary>
    /// <returns>Number of blocks scanned.</returns>
    [Benchmark]
    public int BlockScan()
    {
        ArrayBufferWriter<BlockSpan> writer = new(_markdown.Length / 32);
        return BlockScanner.Scan(_markdown, writer);
    }

    /// <summary>Full markdown render (parse + emit).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int MarkdownRender()
    {
        ArrayBufferWriter<byte> writer = new(_markdown.Length * 2);
        MarkdownRenderer.Render(_markdown, writer);
        return writer.WrittenCount;
    }

    /// <summary>C# syntax-highlight pass.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int LexerCSharp()
    {
        ArrayBufferWriter<byte> sink = new(_csharp.Length * 2);
        HighlightEmitter.Emit(CSharpLexer.Instance, _csharp, sink);
        return sink.WrittenCount;
    }

    /// <summary>End-to-end DocBuilder run against the on-disk corpus (no plugins).</summary>
    /// <returns>Pages processed.</returns>
    [Benchmark]
    public int EndToEndBuild() =>
        new DocBuilder()
            .WithInput(_inputRoot)
            .WithOutput(_outputRoot)
            .BuildAsync()
            .GetAwaiter()
            .GetResult();

    /// <summary>Generates one realistic markdown page.</summary>
    /// <param name="index">Page index for unique anchors.</param>
    /// <returns>Markdown source.</returns>
    private static string Page(int index) =>
        new StringBuilder(1024)
            .Append("# Page ").Append(index).Append('\n').Append('\n')
            .Append("Some intro text with **bold** and `code` and a [link](https://example.com/").Append(index).Append(").\n\n")
            .Append("## Code\n\n```csharp\npublic int Add(int a, int b) => a + b;\n```\n\n")
            .Append("- bullet 1\n- bullet 2\n\n")
            .Append("| h1 | h2 |\n| --- | --- |\n| a | b |\n")
            .ToString();

    /// <summary>Best-effort recursive directory delete.</summary>
    /// <summary>Repeats <paramref name="line"/> <paramref name="count"/> times into a fresh byte array.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="count">Repetition count.</param>
    /// <returns>Concatenated bytes.</returns>
    private static byte[] RepeatBytes(ReadOnlySpan<byte> line, int count)
    {
        var output = new byte[line.Length * count];
        var span = output.AsSpan();
        for (var i = 0; i < count; i++)
        {
            line.CopyTo(span[(i * line.Length)..]);
        }

        return output;
    }

    /// <summary>Best-effort recursive delete used for the temp corpus.</summary>
    /// <param name="path">Directory path.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
