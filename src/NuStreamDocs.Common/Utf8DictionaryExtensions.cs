// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace NuStreamDocs.Common;

/// <summary>UTF-8 byte-keyed <see cref="Dictionary{TKey, TValue}"/> / <see cref="HashSet{T}"/> probe helpers.</summary>
/// <remarks>
/// The .NET 9+ <see cref="Dictionary{TKey, TValue}.AlternateLookup{TAlternateKey}"/> /
/// <see cref="HashSet{T}.AlternateLookup{TAlternateKey}"/> shapes let span-keyed probes
/// hit a byte-array-keyed map without materializing a temporary <c>byte[]</c>, but the
/// call-site syntax is verbose. These extensions wrap the shape so plugin code reads as
/// <c>map.TryGetValueByUtf8(span, out var value)</c> instead of
/// <c>map.GetAlternateLookup&lt;ReadOnlySpan&lt;byte&gt;&gt;().TryGetValue(span, out var value)</c>.
/// <para>
/// The <see cref="Dictionary{TKey, TValue}"/> must be constructed with a comparer that
/// implements <see cref="System.Collections.Generic.IAlternateEqualityComparer{TAlternate, T}"/>
/// for <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> — i.e. <see cref="ByteArrayComparer.Instance"/>.
/// Callers that need to probe many times in a hot loop should cache the alternate-lookup
/// struct directly via <see cref="AsUtf8Lookup{TValue}(Dictionary{byte[], TValue})"/>
/// to avoid the per-call construction.
/// </para>
/// </remarks>
public static class Utf8DictionaryExtensions
{
    /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="dictionary"/>.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <returns>The alternate-lookup struct; cache it once and reuse for hot-path probing.</returns>
    /// <remarks>
    /// For multi-probe paths (per-page renderers, per-token dispatchers) cache the result;
    /// for one-shot probes use the <see cref="TryGetValueByUtf8{TValue}(Dictionary{byte[], TValue}, ReadOnlySpan{byte}, out TValue)"/>
    /// extension instead.
    /// </remarks>
    public static Dictionary<byte[], TValue>.AlternateLookup<ReadOnlySpan<byte>> AsUtf8Lookup<TValue>(this Dictionary<byte[], TValue> dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="set"/>.</summary>
    /// <param name="set">Set built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <returns>The alternate-lookup struct.</returns>
    public static HashSet<byte[]>.AlternateLookup<ReadOnlySpan<byte>> AsUtf8Lookup(this HashSet<byte[]> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="dictionary"/>.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Concurrent dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <returns>The alternate-lookup struct.</returns>
    public static ConcurrentDictionary<byte[], TValue>.AlternateLookup<ReadOnlySpan<byte>> AsUtf8Lookup<TValue>(this ConcurrentDictionary<byte[], TValue> dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    /// <summary>UTF-8-byte-key probe over a byte-array-keyed dictionary.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <param name="value">Value on success.</param>
    /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
    public static bool TryGetValueByUtf8<TValue>(this Dictionary<byte[], TValue> dictionary, ReadOnlySpan<byte> key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out value!);
    }

    /// <summary>UTF-8-byte-key probe over a concurrent byte-array-keyed dictionary.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Concurrent dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <param name="value">Value on success.</param>
    /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
    public static bool TryGetValueByUtf8<TValue>(this ConcurrentDictionary<byte[], TValue> dictionary, ReadOnlySpan<byte> key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out value!);
    }

    /// <summary>UTF-8-byte-key containment check over a byte-array-keyed dictionary.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
    public static bool ContainsKeyByUtf8<TValue>(this Dictionary<byte[], TValue> dictionary, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().ContainsKey(key);
    }

    /// <summary>UTF-8-byte-key containment check over a byte-array-keyed hash set.</summary>
    /// <param name="set">Set built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <returns>True when <paramref name="set"/> contains the key.</returns>
    public static bool ContainsByUtf8(this HashSet<byte[]> set, ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.GetAlternateLookup<ReadOnlySpan<byte>>().Contains(key);
    }
}
