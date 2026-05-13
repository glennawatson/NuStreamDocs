// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace NuStreamDocs.Common;

/// <summary>
/// UTF-8 byte-keyed <see cref="Dictionary{TKey, TValue}"/> / <see cref="HashSet{T}"/> probe
/// helpers. The dictionary or set must be constructed with <see cref="ByteArrayComparer.Instance"/>;
/// for hot-path multi-probe use <see cref="AsUtf8Lookup{TValue}(Dictionary{byte[], TValue})"/>
/// to cache the alternate lookup.
/// </summary>
public static class Utf8DictionaryExtensions
{
    /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="dictionary"/>.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <returns>The alternate-lookup struct; cache it once and reuse for hot-path probing.</returns>
    public static Dictionary<byte[], TValue>.AlternateLookup<ReadOnlySpan<byte>>
        AsUtf8Lookup<TValue>(this Dictionary<byte[], TValue> dictionary) =>
        dictionary.GetAlternateLookup<ReadOnlySpan<byte>>();

    /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="set"/>.</summary>
    /// <param name="set">Set built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <returns>The alternate-lookup struct.</returns>
    public static HashSet<byte[]>.AlternateLookup<ReadOnlySpan<byte>> AsUtf8Lookup(this HashSet<byte[]> set) =>
        set.GetAlternateLookup<ReadOnlySpan<byte>>();

    /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="dictionary"/>.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Concurrent dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <returns>The alternate-lookup struct.</returns>
    public static ConcurrentDictionary<byte[], TValue>.AlternateLookup<ReadOnlySpan<byte>> AsUtf8Lookup<TValue>(
        this ConcurrentDictionary<byte[], TValue> dictionary) => dictionary.GetAlternateLookup<ReadOnlySpan<byte>>();

    /// <summary>UTF-8-byte-key probe over a byte-array-keyed dictionary.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <param name="value">Value on success.</param>
    /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
    public static bool TryGetValueByUtf8<TValue>(
        this Dictionary<byte[], TValue> dictionary,
        ReadOnlySpan<byte> key,
        out TValue value) => dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out value!);

    /// <summary>UTF-8-byte-key probe over a concurrent byte-array-keyed dictionary.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Concurrent dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <param name="value">Value on success.</param>
    /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
    public static bool TryGetValueByUtf8<TValue>(
        this ConcurrentDictionary<byte[], TValue> dictionary,
        ReadOnlySpan<byte> key,
        out TValue value) =>
        dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out value!);

    /// <summary>UTF-8-byte-key containment check over a byte-array-keyed dictionary.</summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="dictionary">Dictionary built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
    public static bool ContainsKeyByUtf8<TValue>(this Dictionary<byte[], TValue> dictionary, ReadOnlySpan<byte> key) =>
        dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().ContainsKey(key);

    /// <summary>UTF-8-byte-key containment check over a byte-array-keyed hash set.</summary>
    /// <param name="set">Set built with <see cref="ByteArrayComparer.Instance"/>.</param>
    /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
    /// <returns>True when <paramref name="set"/> contains the key.</returns>
    public static bool ContainsByUtf8(this HashSet<byte[]> set, ReadOnlySpan<byte> key) =>
        set.GetAlternateLookup<ReadOnlySpan<byte>>().Contains(key);
}
