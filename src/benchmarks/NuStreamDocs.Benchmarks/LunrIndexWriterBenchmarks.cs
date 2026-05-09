// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Search;
using NuStreamDocs.Search.Lunr;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// End-to-end <c>LunrIndexWriter.Write</c> for a representative document corpus — measures the
/// finalize-stage cost (one invocation per build, but on a 14 K-page corpus it dominates the
/// search plugin's phase-time line).
/// </summary>
/// <remarks>
/// Writes to a per-instance temp directory so the I/O path is exercised the same way the real
/// pipeline hits it. Disposed by the <c>[GlobalCleanup]</c> teardown; runs across two corpus
/// sizes (small / mid) so a quadratic regression in the writer is visible.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class LunrIndexWriterBenchmarks
{
    /// <summary>Document body length (bytes) — mid-size pages typical of an SDK doc.</summary>
    private const int BodyBytes = 1024;

    /// <summary>Bytes per repeated sentence in the body — sized so 1024-byte body packs cleanly.</summary>
    private const int BytesPerSentence = 32;

    /// <summary>Document count for the small fixture.</summary>
    private const int SmallDocCount = 50;

    /// <summary>Document count for the mid fixture.</summary>
    private const int MidDocCount = 500;

    /// <summary>Per-instance temp root for the JSON output files.</summary>
    private string _tempRoot = string.Empty;

    /// <summary>Pre-built small corpus.</summary>
    private SearchDocument[] _smallCorpus = [];

    /// <summary>Pre-built mid corpus.</summary>
    private SearchDocument[] _midCorpus = [];

    /// <summary>Gets or sets the document count for the corpus the iteration writes.</summary>
    [Params(SmallDocCount, MidDocCount)]
    public int Documents { get; set; }

    /// <summary>Builds the corpora + temp output dir once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-lunr-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(_tempRoot);
        _smallCorpus = BuildCorpus(SmallDocCount);
        _midCorpus = BuildCorpus(MidDocCount);
    }

    /// <summary>Tears down the temp dir.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; don't fail the benchmark teardown.
        }
    }

    /// <summary>Writes the configured corpus through <see cref="LunrIndexWriter"/>.</summary>
    /// <returns>Path of the just-written file (kept so the JIT can't elide the call).</returns>
    [Benchmark]
    public string Write()
    {
        var corpus = Documents <= SmallDocCount ? _smallCorpus : _midCorpus;
        var path = Path.Combine(_tempRoot, "search_index_" + Documents + ".json");
        LunrIndexWriter.Write(path, "en", corpus);
        return path;
    }

    /// <summary>Builds <paramref name="count"/> SearchDocument records with deterministic content.</summary>
    /// <param name="count">Document count.</param>
    /// <returns>Pre-encoded UTF-8 corpus.</returns>
    private static SearchDocument[] BuildCorpus(int count)
    {
        var bodyBlob = new StringBuilder(BodyBytes + BytesPerSentence);
        for (var i = 0; i < BodyBytes / BytesPerSentence; i++)
        {
            bodyBlob.Append("body sentence with searchable words. ");
        }

        var bodyBytes = Encoding.UTF8.GetBytes(bodyBlob.ToString());
        var corpus = new SearchDocument[count];
        for (var i = 0; i < count; i++)
        {
            var url = Encoding.UTF8.GetBytes("/page-" + i.ToString(CultureInfo.InvariantCulture) + ".html");
            var title = Encoding.UTF8.GetBytes("Page " + i.ToString(CultureInfo.InvariantCulture));
            corpus[i] = new(url, title, bodyBytes);
        }

        return corpus;
    }
}
