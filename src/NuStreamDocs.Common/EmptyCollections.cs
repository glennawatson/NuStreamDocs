// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Common;

/// <summary>
/// Shared empty <see cref="Dictionary{TKey, TValue}"/> and <see cref="HashSet{T}"/> singletons.
/// The returned instances must be treated as read-only — mutating them corrupts every other consumer.
/// </summary>
public static class EmptyCollections
{
    /// <summary>Returns the cached empty <see cref="Dictionary{TKey, TValue}"/> for <typeparamref name="TKey"/>/<typeparamref name="TValue"/>. Do not mutate.</summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    /// <returns>The shared empty dictionary singleton.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S4018:Generic methods should provide type parameters",
        Justification = "Caller-supplied type parameters are the whole API surface — there is no value parameter to drive inference.")]
    public static Dictionary<TKey, TValue> DictionaryFor<TKey, TValue>()
        where TKey : notnull => EmptyDictionaryHolder<TKey, TValue>.Instance;

    /// <summary>Returns the cached empty <see cref="HashSet{T}"/> for <typeparamref name="T"/>. Do not mutate.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <returns>The shared empty set singleton.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S4018:Generic methods should provide type parameters",
        Justification = "Caller-supplied type parameter is the whole API surface — there is no value parameter to drive inference.")]
    public static HashSet<T> HashSetFor<T>() => EmptyHashSetHolder<T>.Instance;

    /// <summary>Singleton holder for the empty dictionary per type pair.</summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    private static class EmptyDictionaryHolder<TKey, TValue>
        where TKey : notnull
    {
        /// <summary>Shared empty dictionary instance.</summary>
        public static readonly Dictionary<TKey, TValue> Instance = [];
    }

    /// <summary>Singleton holder for the empty set per element type.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    private static class EmptyHashSetHolder<T>
    {
        /// <summary>Shared empty set instance.</summary>
        public static readonly HashSet<T> Instance = [];
    }
}
