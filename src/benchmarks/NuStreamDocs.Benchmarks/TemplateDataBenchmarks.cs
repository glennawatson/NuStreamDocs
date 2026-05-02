// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
using NuStreamDocs.Templating;

namespace NuStreamDocs.Benchmarks;

/// <summary>Per-token cost of the byte-keyed <see cref="TemplateData"/> scalar / section probes.</summary>
/// <remarks>
/// <see cref="TemplateData"/> stores its scalar and section maps as <c>Dictionary&lt;byte[], …&gt;</c>
/// with <see cref="ByteArrayComparer.Instance"/>; the renderer's per-token resolve uses the cached
/// <see cref="Dictionary{TKey, TValue}.AlternateLookup{TAlternateKey}"/> shape so the probe never
/// allocates a string. These benchmarks measure that probe in isolation.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class TemplateDataBenchmarks
{
    /// <summary>Small site — typical theme template scalar count.</summary>
    private const int SmallEntries = 16;

    /// <summary>Larger site — covers feature-rich themes with extras.</summary>
    private const int LargeEntries = 64;

    /// <summary>Pre-built data scope sized by the iteration parameter.</summary>
    private TemplateData _data = TemplateData.Empty;

    /// <summary>Pre-encoded UTF-8 keys hit by the resolve loops; one per scalar.</summary>
    private byte[][] _keys = [];

    /// <summary>Pre-built UTF-8 keys for the miss-path benchmark — each is its hit-key + a sentinel byte to force a miss.</summary>
    private byte[][] _missKeys = [];

    /// <summary>Gets or sets the synthetic scalar count for the current iteration.</summary>
    [Params(SmallEntries, LargeEntries)]
    public int EntryCount { get; set; }

    /// <summary>Builds the data scope and probe-key list.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var scalars = new Dictionary<byte[], ReadOnlyMemory<byte>>(EntryCount, ByteArrayComparer.Instance);
        _keys = new byte[EntryCount][];
        _missKeys = new byte[EntryCount][];
        for (var i = 0; i < EntryCount; i++)
        {
            var idx = i.ToString(CultureInfo.InvariantCulture);
            var keyBytes = Encoding.UTF8.GetBytes("scalar_" + idx);
            _keys[i] = keyBytes;

            // Miss key = real key + sentinel suffix that forces a miss without allocating in the bench loop.
            var missKey = new byte[keyBytes.Length + 1];
            keyBytes.CopyTo(missKey, 0);
            missKey[keyBytes.Length] = (byte)'X';
            _missKeys[i] = missKey;

            scalars[keyBytes] = Encoding.UTF8.GetBytes("value-" + idx);
        }

        _data = new(scalars, sections: null);
    }

    /// <summary>Resolves every key in <see cref="_keys"/> via the byte-keyed alt-lookup probe.</summary>
    /// <returns>Sum of resolved value lengths (defeats DCE).</returns>
    [Benchmark]
    public int ResolveScalarsByBytes()
    {
        var total = 0;
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_data.TryGetScalar(_keys[i], out var value))
            {
                total += value.Length;
            }
        }

        return total;
    }

    /// <summary>Worst-case miss path: probes every key with a guaranteed non-match.</summary>
    /// <returns>Number of misses (always equal to <see cref="_keys"/>.Length).</returns>
    [Benchmark]
    public int ResolveScalarMisses()
    {
        var misses = 0;
        for (var i = 0; i < _missKeys.Length; i++)
        {
            if (!_data.TryGetScalar(_missKeys[i], out _))
            {
                misses++;
            }
        }

        return misses;
    }

    /// <summary>Reports the configured truthiness of every key.</summary>
    /// <returns>Number of truthy entries (always equal to <see cref="_keys"/>.Length).</returns>
    [Benchmark]
    public int IsTruthyForAllKeys()
    {
        var truthy = 0;
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_data.IsTruthy(_keys[i]))
            {
                truthy++;
            }
        }

        return truthy;
    }
}
