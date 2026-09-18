// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.SuperFences;

namespace NuStreamDocs.Benchmarks;

/// <summary>Per-page cost of the SuperFences dispatcher across registered / unregistered fence mixes.</summary>
[DebuggerDisplay("SuperFencesDispatchBenchmarks: PageSizeKb={PageSizeKb}, FencesPer200Bytes={FencesPer200Bytes}")]
[ShortRunJob]
[MemoryDiagnoser]
public class SuperFencesDispatchBenchmarks
{
    /// <summary>Bytes per kilobyte; converts <c>PageSizeKb</c> into a byte count.</summary>
    private const int BytesPerKb = 1024;

    /// <summary>Smallest synthesized page (~4 KB).</summary>
    private const int SmallPageKb = 4;

    /// <summary>Mid-range synthesized page (~32 KB).</summary>
    private const int MediumPageKb = 32;

    /// <summary>Sparse fence density.</summary>
    private const int LowDensity = 4;

    /// <summary>Heavy fence density (API-reference page).</summary>
    private const int HighDensity = 16;

    /// <summary>Number of fence shapes the synthesizer cycles through.</summary>
    private const int FenceShapes = 3;

    /// <summary>Body-byte interval used to define fence density.</summary>
    private const int DensityIntervalBytes = 200;

    /// <summary>Number of languages with custom rendering handlers.</summary>
    private const int RegisteredLanguageCount = 2;

    /// <summary>Pre-built page bytes for the current iteration.</summary>
    private byte[] _html = [];

    /// <summary>Span-keyed alt lookup with both fence languages registered.</summary>
    private Dictionary<byte[], ICustomFenceHandler>.AlternateLookup<ReadOnlySpan<byte>> _allRegistered;

    /// <summary>Span-keyed alt lookup with no fence languages registered (every fence falls through verbatim).</summary>
    private Dictionary<byte[], ICustomFenceHandler>.AlternateLookup<ReadOnlySpan<byte>> _noneRegistered;

    /// <summary>Gets or sets the synthetic page size in kilobytes.</summary>
    [Params(SmallPageKb, MediumPageKb)]
    public int PageSizeKb { get; set; }

    /// <summary>Gets or sets the synthetic fence density (fences per ~200 bytes of body text).</summary>
    [Params(LowDensity, HighDensity)]
    public int FencesPer200Bytes { get; set; }

    /// <summary>Gets the prose inserted between fenced blocks.</summary>
    private static ApiCompatString Filler => "<p>Lorem ipsum dolor sit amet.</p>";

    /// <summary>Generates the HTML fixture for the current params.</summary>
    [GlobalSetup]
    public void Setup()
    {
        StringBuilder sb = new(PageSizeKb * BytesPerKb);
        var totalBytes = PageSizeKb * BytesPerKb;
        var blockEvery = DensityIntervalBytes / Math.Max(1, FencesPer200Bytes);
        var idx = 0;
        var written = 0;
        while (written < totalBytes)
        {
            var i = new ApiCompatString(idx.ToString(CultureInfo.InvariantCulture));
            var emitted = new ApiCompatString((idx % FenceShapes) switch
            {
                0 => StringCompose.Concat("<pre><code class=\"language-mermaid\">graph", i, "</code></pre>"),
                1 => StringCompose.Concat("<pre><code class=\"language-math\">x_", i, " = 1</code></pre>"),
                _ => StringCompose.Concat("<pre><code class=\"language-csharp\">var x", i, " = 1;</code></pre>")
            });
            _ = sb.Append(emitted.ToStringValue());
            written += emitted.ToStringValue().Length;

            for (var f = 0; f < blockEvery && written < totalBytes; f++)
            {
                _ = sb.Append(Filler.ToStringValue());
                written += Filler.ToStringValue().Length;
            }

            idx++;
        }

        _html = Encoding.UTF8.GetBytes(sb.ToString());

        Dictionary<byte[], ICustomFenceHandler> fullDict = [with(RegisteredLanguageCount, ByteArrayComparer.Instance)];
        fullDict[[.. "mermaid"u8]] = new StubMermaidHandler();
        fullDict[[.. "math"u8]] = new StubMathHandler();
        _allRegistered = fullDict.AsUtf8Lookup();

        Dictionary<byte[], ICustomFenceHandler> emptyDict = [with(0, ByteArrayComparer.Instance)];
        _noneRegistered = emptyDict.AsUtf8Lookup();
    }

    /// <summary>Dispatch with both languages registered — exercises the full byte-keyed lookup + handler-render path on every fence.</summary>
    /// <returns>The byte count of the rewritten output.</returns>
    [Benchmark]
    public int DispatchAllRegistered()
    {
        ArrayBufferWriter<byte> sink = new(_html.Length);
        if (!SuperFencesDispatcher.DispatchInto(_html, _allRegistered, sink))
        {
            sink.Write(_html);
        }

        return sink.WrittenCount;
    }

    /// <summary>
    /// Dispatch with no languages registered — every fence's language
    /// hash misses the empty dict and the dispatcher emits zero
    /// replacement bytes (byte-fast-reject path).
    /// </summary>
    /// <returns>The byte count of the rewritten output.</returns>
    [Benchmark]
    public int DispatchNoneRegistered()
    {
        ArrayBufferWriter<byte> sink = new(_html.Length);
        if (!SuperFencesDispatcher.DispatchInto(_html, _noneRegistered, sink))
        {
            sink.Write(_html);
        }

        return sink.WrittenCount;
    }

    /// <summary>Stub handler that wraps the body in a div.</summary>
    private sealed class StubMermaidHandler : ICustomFenceHandler
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => "mermaid"u8;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            writer.Write("<div class=\"mermaid\">"u8);
            writer.Write(content);
            writer.Write("</div>"u8);
        }
    }

    /// <summary>Stub math handler.</summary>
    private sealed class StubMathHandler : ICustomFenceHandler
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => "math"u8;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            writer.Write("<span class=\"math\">"u8);
            writer.Write(content);
            writer.Write("</span>"u8);
        }
    }
}
