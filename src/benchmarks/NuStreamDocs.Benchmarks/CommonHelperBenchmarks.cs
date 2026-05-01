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
[MemoryDiagnoser]
public class CommonHelperBenchmarks
{
    /// <summary>Per-iteration string-write count for the Utf8StringWriter pass.</summary>
    private const int Utf8WriterRepetitions = 1_000;

    /// <summary>Repeat factor for the snapshot / shortcode payloads.</summary>
    private const int PayloadRepetitions = 64;

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

    /// <summary>Initial-state setup for the per-iteration buffers.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _title = "The quick brown fox jumps over the lazy dog & friends \"on the run\"";
        _snapshotInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            "<h1>Heading</h1><p>One paragraph with <em>emphasis</em> and <code>code</code>.</p>",
            PayloadRepetitions)));
        _xmlEscapeInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            "Tom & Jerry <said> \"hi\" 'there' & 'again' >> end ",
            XmlPayloadRepetitions)));
        _shortcodeInput = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(
            "the :rocket: ship sailed past :material-anchor: at :+1: speed and we said :wave_hand: ",
            PayloadRepetitions)));
    }

    /// <summary>Bulk UTF-8 string write into a pre-sized writer.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Utf8StringWriter_String()
    {
        var writer = new ArrayBufferWriter<byte>(_title.Length * Utf8WriterRepetitions * 2);
        for (var i = 0; i < Utf8WriterRepetitions; i++)
        {
            Utf8StringWriter.Write(writer, _title);
        }

        return writer.WrittenCount;
    }

    /// <summary>Repeated int writes — exercises the stackalloc + TryFormat path.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Utf8StringWriter_Int32()
    {
        var writer = new ArrayBufferWriter<byte>(Utf8WriterRepetitions * 8);
        for (var i = 0; i < Utf8WriterRepetitions; i++)
        {
            Utf8StringWriter.WriteInt32(writer, i);
        }

        return writer.WrittenCount;
    }

    /// <summary>XML mode entity escape (skips quotes).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int XmlEntityEscaper_XmlMode()
    {
        var writer = new ArrayBufferWriter<byte>(_xmlEscapeInput.Length * 2);
        XmlEntityEscaper.WriteEscaped(writer, _xmlEscapeInput, XmlEntityEscaper.Mode.Xml);
        return writer.WrittenCount;
    }

    /// <summary>HTML attribute mode entity escape (escapes quotes too).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int XmlEntityEscaper_HtmlAttributeMode()
    {
        var writer = new ArrayBufferWriter<byte>(_xmlEscapeInput.Length * 2);
        XmlEntityEscaper.WriteEscaped(writer, _xmlEscapeInput, XmlEntityEscaper.Mode.HtmlAttribute);
        return writer.WrittenCount;
    }

    /// <summary>Snapshot-then-rewrite over a sample HTML buffer.</summary>
    /// <returns>Bytes written by the rewrite pass.</returns>
    [Benchmark]
    public int HtmlSnapshotRewriter_FullPage()
    {
        var writer = new ArrayBufferWriter<byte>(_snapshotInput.Length);
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
