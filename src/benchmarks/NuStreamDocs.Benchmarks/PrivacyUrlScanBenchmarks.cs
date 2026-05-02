// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Privacy;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-page cost of the Privacy plugin's byte-only URL audit + rewrite
/// pipelines, parameterized over page size, URL density, and the
/// fraction of URLs the host filter rejects. The deferred-decode win
/// is most visible at high reject rates — this benchmark makes that
/// visible.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class PrivacyUrlScanBenchmarks
{
    /// <summary>Bytes per kilobyte; converts <c>PageSizeKb</c> into a byte count.</summary>
    private const int BytesPerKb = 1024;

    /// <summary>Smallest synthesized page (~4 KB).</summary>
    private const int SmallPageKb = 4;

    /// <summary>Mid-range synthesized page (~32 KB).</summary>
    private const int MediumPageKb = 32;

    /// <summary>Sparse URL density.</summary>
    private const int LowDensity = 4;

    /// <summary>Heavy URL density (CDN-rich page).</summary>
    private const int HighDensity = 16;

    /// <summary>Number of element shapes the synthesizer cycles through (img / link / srcset / inline-style url() / heading).</summary>
    private const int ElementShapes = 5;

    /// <summary>Pre-built page bytes for the current iteration.</summary>
    private byte[] _html = [];

    /// <summary>Filter that accepts every host — exercises the full decode + register path.</summary>
    private HostFilter _acceptAll = null!;

    /// <summary>Filter that rejects every host — exercises the byte-only fast-reject path.</summary>
    private HostFilter _rejectAll = null!;

    /// <summary>Pre-built registry shared across the rewrite benchmarks.</summary>
    private ExternalAssetRegistry _registry = null!;

    /// <summary>Gets or sets the synthetic page size in kilobytes.</summary>
    [Params(SmallPageKb, MediumPageKb)]
    public int PageSizeKb { get; set; }

    /// <summary>Gets or sets the synthetic URL density (URLs per ~200 bytes of body text).</summary>
    [Params(LowDensity, HighDensity)]
    public int UrlsPer200Bytes { get; set; }

    /// <summary>Generates the HTML fixture for the current params.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var sb = new StringBuilder(PageSizeKb * BytesPerKb);
        var totalBytes = PageSizeKb * BytesPerKb;
        var blockEvery = 200 / Math.Max(1, UrlsPer200Bytes);
        var idx = 0;
        var written = 0;
        while (written < totalBytes)
        {
            var i = idx.ToString(CultureInfo.InvariantCulture);
            var emitted = (idx % ElementShapes) switch
            {
                0 => $"<img src=\"https://cdn.example/img{i}.png\">",
                1 => $"<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/c{i}\">",
                2 => $"<img srcset=\"https://cdn.example/x{i}.png 2x, https://cdn.example/y{i}.png 1x\">",
                3 => $"<style>.b{i} {{ background: url(https://cdn.example/bg{i}.png); }}</style>",
                _ => $"<h2 id=\"section-{i}\">Section {i}</h2>",
            };
            sb.Append(emitted);
            written += emitted.Length;

            for (var f = 0; f < blockEvery && written < totalBytes; f++)
            {
                const string Filler = "<p>Lorem ipsum dolor sit amet.</p>";
                sb.Append(Filler);
                written += Filler.Length;
            }

            idx++;
        }

        _html = Encoding.UTF8.GetBytes(sb.ToString());
        _acceptAll = new(hostsToSkip: null, hostsAllowed: null);
        _rejectAll = new(hostsToSkip: ["cdn.example", "fonts.googleapis.com"], hostsAllowed: null);
        _registry = new("assets/external");
    }

    /// <summary>Audit pass with a filter that accepts every host — every URL is decoded + registered.</summary>
    /// <returns>The audited URL count.</returns>
    [Benchmark]
    public int AuditAcceptAll()
    {
        var set = new ConcurrentDictionary<byte[], byte>(ByteArrayComparer.Instance);
        ExternalUrlScanner.Audit(_html, _acceptAll, set);
        return set.Count;
    }

    /// <summary>Audit pass with a filter that rejects every host — exercises the byte-only fast-reject path. Should allocate dramatically less than <see cref="AuditAcceptAll"/>.</summary>
    /// <returns>The audited URL count (always zero on this filter).</returns>
    [Benchmark]
    public int AuditRejectAll()
    {
        var set = new ConcurrentDictionary<byte[], byte>(ByteArrayComparer.Instance);
        ExternalUrlScanner.Audit(_html, _rejectAll, set);
        return set.Count;
    }

    /// <summary>Rewrite pass with a filter that accepts every host — exercises the full byte-keyed registry path.</summary>
    /// <returns>The rewritten byte count.</returns>
    [Benchmark]
    public int RewriteAcceptAll() =>
        ExternalUrlScanner.Rewrite(_html, _registry, _acceptAll).Length;

    /// <summary>Rewrite pass with a filter that rejects every host — short-circuits to the verbatim-copy path.</summary>
    /// <returns>The rewritten byte count.</returns>
    [Benchmark]
    public int RewriteRejectAll() =>
        ExternalUrlScanner.Rewrite(_html, _registry, _rejectAll).Length;
}
