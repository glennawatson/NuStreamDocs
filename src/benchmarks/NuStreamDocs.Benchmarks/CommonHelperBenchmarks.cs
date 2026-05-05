// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Throughput + alloc benchmarks for the dedup helpers extracted in
/// the recent refactor: <c>Utf8StringWriter</c>,
/// <c>XmlEntityEscaper</c>, <c>HtmlSnapshotRewriter</c>, and
/// <c>ShortcodeScanner</c>. These all sit on the per-page hot path,
/// so the benchmarks confirm the dedup didn't introduce delegate
/// boxing, extra copies, or per-call allocations.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class CommonHelperBenchmarks
{
    /// <summary>Per-iteration string-write count for the Utf8StringWriter pass.</summary>
    private const int Utf8WriterRepetitions = 1_000;

    /// <summary>Headroom factor for the string-writer's pre-sized backing array (UTF-8 encode is at most 2× UTF-16 char count for non-ASCII).</summary>
    private const int StringWriterCapacityFactor = 2;

    /// <summary>Bytes per int when sizing the int-writer's backing array (max digits in <see cref="int.MaxValue"/> + sign).</summary>
    private const int IntWriterBytesPerEntry = 8;

    /// <summary>Repeat factor for the snapshot / shortcode payloads.</summary>
    private const int PayloadRepetitions = 64;

    /// <summary>Probe count for the run-length micro-benchmark — drives the loop hot enough to time without saturating BDN error bars.</summary>
    private const int RunLengthProbes = 10_000;

    /// <summary>Repeat factor for the XML-escape payload (denser per-line).</summary>
    private const int XmlPayloadRepetitions = 128;

    /// <summary>Sample HTML buffer reused across snapshot-rewrite iterations.</summary>
    private byte[] _snapshotInput = [];

    /// <summary>XML payload used by both entity-escape modes.</summary>
    private byte[] _xmlEscapeInput = [];

    /// <summary>Markdown payload that feeds the shortcode scanner.</summary>
    private byte[] _shortcodeInput = [];

    /// <summary>Sample title text reused by Utf8StringWriter benchmarks.</summary>
    private string _title = string.Empty;

    /// <summary>Synthetic UTF-8 buffer of leading backticks for the <c>RunLength</c> micro-benchmark.</summary>
    private byte[] _runLengthInput = [];

    /// <summary>
    /// Reused writer for the Utf8StringWriter benchmarks — constructed
    /// once in <see cref="Setup"/> so the per-invocation measurement
    /// reflects the actual write path, not the cost of allocating a
    /// fresh backing array for every benchmark invocation.
    /// </summary>
    private ArrayBufferWriter<byte> _stringWriter = null!;

    /// <summary>Reused writer for the Utf8StringWriter int benchmark.</summary>
    private ArrayBufferWriter<byte> _intWriter = null!;

    /// <summary>Initial-state setup for the per-iteration buffers.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _title = "The quick brown fox jumps over the lazy dog & friends \"on the run\"";
        _stringWriter = new(_title.Length * Utf8WriterRepetitions * StringWriterCapacityFactor);
        _intWriter = new(Utf8WriterRepetitions * IntWriterBytesPerEntry);
        _snapshotInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            "<h1>Heading</h1><p>One paragraph with <em>emphasis</em> and <code>code</code>.</p>",
            PayloadRepetitions)));
        _xmlEscapeInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            "Tom & Jerry <said> \"hi\" 'there' & 'again' >> end ",
            XmlPayloadRepetitions)));
        _shortcodeInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            "the :rocket: ship sailed past :material-anchor: at :+1: speed and we said :wave_hand: ",
            PayloadRepetitions)));

        // Fixture sized to the typical fence/marker run encountered by the inline pass.
        _runLengthInput = [.. "```bash code"u8];
    }

    /// <summary>Bulk UTF-8 string write into a pre-sized writer reused across invocations.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Utf8StringWriter_String()
    {
        _stringWriter.ResetWrittenCount();
        for (var i = 0; i < Utf8WriterRepetitions; i++)
        {
            Utf8StringWriter.Write(_stringWriter, _title);
        }

        return _stringWriter.WrittenCount;
    }

    /// <summary>Counts the leading marker run repeatedly — exercises the inline pass's fence/emphasis run-length probe.</summary>
    /// <returns>Accumulated run lengths, kept live so the JIT doesn't elide the call.</returns>
    [Benchmark]
    public int AsciiByteHelpers_RunLength()
    {
        var total = 0;
        for (var i = 0; i < RunLengthProbes; i++)
        {
            total += AsciiByteHelpers.RunLength(_runLengthInput, 0, (byte)'`');
        }

        return total;
    }

    /// <summary>Repeated int writes — exercises the stackalloc + TryFormat path. Writer is reused across invocations.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Utf8StringWriter_Int32()
    {
        _intWriter.ResetWrittenCount();
        for (var i = 0; i < Utf8WriterRepetitions; i++)
        {
            Utf8StringWriter.WriteInt32(_intWriter, i);
        }

        return _intWriter.WrittenCount;
    }

    /// <summary>XML mode entity escape (skips quotes).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int XmlEntityEscaper_XmlMode()
    {
        ArrayBufferWriter<byte> writer = new(_xmlEscapeInput.Length * 2);
        XmlEntityEscaper.WriteEscaped(writer, _xmlEscapeInput, XmlEntityEscaper.Mode.Xml);
        return writer.WrittenCount;
    }

    /// <summary>HTML attribute mode entity escape (escapes quotes too).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int XmlEntityEscaper_HtmlAttributeMode()
    {
        ArrayBufferWriter<byte> writer = new(_xmlEscapeInput.Length * 2);
        XmlEntityEscaper.WriteEscaped(writer, _xmlEscapeInput, XmlEntityEscaper.Mode.HtmlAttribute);
        return writer.WrittenCount;
    }

    /// <summary>Snapshot-then-rewrite over a sample HTML buffer.</summary>
    /// <returns>Bytes written by the rewrite pass.</returns>
    [Benchmark]
    public int HtmlSnapshotRewriter_FullPage()
    {
        ArrayBufferWriter<byte> writer = new(_snapshotInput.Length);
        writer.Write(_snapshotInput);
        HtmlSnapshotRewriter.Rewrite(
            writer,
            state: 0,
            static (snapshot, dst, _) => dst.Write(snapshot));
        return writer.WrittenCount;
    }

    /// <summary>Shortcode scan over a markdown buffer with mixed shortcodes.</summary>
    /// <returns>Number of shortcode bodies located.</returns>
    [Benchmark]
    public int ShortcodeScanner_MixedPayload()
    {
        var input = _shortcodeInput;
        var hits = 0;
        var cursor = 0;
        while (cursor < input.Length)
        {
            if (input[cursor] is not (byte)':')
            {
                cursor++;
                continue;
            }

            var bodyStart = cursor + 1;
            if (bodyStart >= input.Length || !ShortcodeScanner.IsBodyByte(input[bodyStart]))
            {
                cursor++;
                continue;
            }

            var bodyEnd = ShortcodeScanner.ScanBody(input, bodyStart);
            if (bodyEnd < input.Length && input[bodyEnd] is (byte)':')
            {
                hits++;
                cursor = bodyEnd + 1;
            }
            else
            {
                cursor = bodyEnd;
            }
        }

        return hits;
    }
}
