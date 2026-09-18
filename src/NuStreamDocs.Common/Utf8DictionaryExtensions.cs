// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.Common;

/// <summary>
/// UTF-8 byte-keyed <see cref="Dictionary{TKey, TValue}"/> / <see cref="HashSet{T}"/> probe
/// helpers. The dictionary or set must be constructed with <see cref="ByteArrayComparer.Instance"/>;
/// for hot-path multi-probe use <see cref="AsUtf8Lookup{TValue}(Dictionary{byte[], TValue})"/>
/// to cache the alternate lookup.
/// </summary>
public static class Utf8DictionaryExtensions
{
    /// <summary>Extension members for <c>ConcurrentDictionary&lt;byte[], TValue&gt;</c>.</summary>
    /// <typeparam name="TValue">Stored value type.</typeparam>
    /// <param name="dictionary">Value to operate on.</param>
    extension<TValue>(ConcurrentDictionary<byte[], TValue> dictionary)
    {

        /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="dictionary"/>.</summary>
        /// <returns>The alternate-lookup struct.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConcurrentDictionary<byte[], TValue>.AlternateLookup<ReadOnlySpan<byte>> AsUtf8Lookup() =>
            dictionary.GetAlternateLookup<ReadOnlySpan<byte>>();

        /// <summary>UTF-8-byte-key probe over a concurrent byte-array-keyed dictionary.</summary>
        /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
        /// <param name="value">Value on success.</param>
        /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValueByUtf8(
            ReadOnlySpan<byte> key,
            out TValue value) =>
            dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out value!);
    }

    /// <summary>Extension members for <c>Dictionary&lt;byte[], TValue&gt;</c>.</summary>
    /// <typeparam name="TValue">Stored value type.</typeparam>
    /// <param name="dictionary">Value to operate on.</param>
    extension<TValue>(Dictionary<byte[], TValue> dictionary)
    {
        /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="dictionary"/>.</summary>
        /// <returns>The alternate-lookup struct; cache it once and reuse for hot-path probing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Dictionary<byte[], TValue>.AlternateLookup<ReadOnlySpan<byte>>
            AsUtf8Lookup() =>
            dictionary.GetAlternateLookup<ReadOnlySpan<byte>>();

        /// <summary>UTF-8-byte-key probe over a byte-array-keyed dictionary.</summary>
        /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
        /// <param name="value">Value on success.</param>
        /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValueByUtf8(
            ReadOnlySpan<byte> key,
            out TValue value) => dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out value!);

        /// <summary>UTF-8-byte-key containment check over a byte-array-keyed dictionary.</summary>
        /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
        /// <returns>True when <paramref name="dictionary"/> contains the key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKeyByUtf8(ReadOnlySpan<byte> key) =>
            dictionary.GetAlternateLookup<ReadOnlySpan<byte>>().ContainsKey(key);
    }

    /// <summary>Extension members for <c>HashSet&lt;byte[]&gt;</c>.</summary>
    /// <param name="set">Value to operate on.</param>
    extension(HashSet<byte[]> set)
    {

        /// <summary>Returns the cached <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> alternate lookup over <paramref name="set"/>.</summary>
        /// <returns>The alternate-lookup struct.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HashSet<byte[]>.AlternateLookup<ReadOnlySpan<byte>> AsUtf8Lookup() =>
            set.GetAlternateLookup<ReadOnlySpan<byte>>();

        /// <summary>UTF-8-byte-key containment check over a byte-array-keyed hash set.</summary>
        /// <param name="key">UTF-8 key bytes; not materialized to <see cref="byte"/>[].</param>
        /// <returns>True when <paramref name="set"/> contains the key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsByUtf8(ReadOnlySpan<byte> key) =>
            set.GetAlternateLookup<ReadOnlySpan<byte>>().Contains(key);
    }
}
