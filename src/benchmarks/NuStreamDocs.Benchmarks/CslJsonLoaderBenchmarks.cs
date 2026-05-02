// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Bibliography.Csl;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the streaming <see cref="CslJsonLoader"/> parse path.</summary>
/// <remarks>
/// The loader was rewritten from <see cref="System.Text.Json.JsonDocument"/> + <see cref="System.Text.Json.JsonElement"/>
/// (DOM, every string value materialized as <see cref="string"/>) to <see cref="System.Text.Json.Utf8JsonReader"/> +
/// <see cref="System.Text.Json.Utf8JsonReader.CopyString(System.Span{byte})"/> (streaming, every string value lands directly
/// as a <see cref="byte"/> array). These benchmarks pin the resulting per-entry parse cost.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class CslJsonLoaderBenchmarks
{
    /// <summary>Small bibliography — typical single-page footnote pile.</summary>
    private const int SmallEntries = 25;

    /// <summary>Mid-sized bibliography — typical site-wide reference set.</summary>
    private const int MidEntries = 250;

    /// <summary>Base year for the synthetic <c>issued</c> dates.</summary>
    private const int BaseYear = 2000;

    /// <summary>Year-spread modulus so synthetic entries cycle through a 25-year window.</summary>
    private const int YearSpread = 25;

    /// <summary>Pre-built UTF-8 JSON fixture for the current iteration.</summary>
    private byte[] _json = [];

    /// <summary>Gets or sets the synthetic entry count for the current iteration.</summary>
    [Params(SmallEntries, MidEntries)]
    public int EntryCount { get; set; }

    /// <summary>Generates the JSON fixture for the current <see cref="EntryCount"/>.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder(EntryCount * 256)
            .Append('[');
        for (var i = 0; i < EntryCount; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var idx = i.ToString(CultureInfo.InvariantCulture);
            sb.Append("{\"id\":\"entry-").Append(idx).Append('"')
                .Append(",\"type\":\"book\"")
                .Append(",\"title\":\"Synthetic Citation Title ").Append(idx).Append('"')
                .Append(",\"author\":[{\"family\":\"Family").Append(idx)
                .Append("\",\"given\":\"Given").Append(idx).Append("\"}]")
                .Append(",\"issued\":{\"date-parts\":[[").Append(BaseYear + (i % YearSpread)).Append("]]}")
                .Append(",\"publisher\":\"Publisher ").Append(idx).Append('"')
                .Append(",\"container-title\":\"Journal of Things\"")
                .Append(",\"volume\":\"").Append(idx).Append('"')
                .Append(",\"page\":\"").Append(idx).Append('-').Append(idx).Append("9\"")
                .Append('}');
        }

        sb.Append(']');
        _json = Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>Parses the full fixture via the streaming <c>CslJsonLoader.Parse</c>.</summary>
    /// <returns>The resolved entry count.</returns>
    [Benchmark]
    public int ParseStreaming() => CslJsonLoader.Parse(_json).Count;
}
