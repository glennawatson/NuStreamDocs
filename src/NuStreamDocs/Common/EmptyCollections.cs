// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Common;

/// <summary>
/// Cached, alloc-free empty <see cref="Dictionary{TKey, TValue}"/> and
/// <see cref="HashSet{T}"/> singletons — drop-in replacements for the
/// removed <c>FrozenDictionary&lt;,&gt;.Empty</c> / <c>FrozenSet&lt;&gt;.Empty</c>
/// sentinels.
/// </summary>
/// <remarks>
/// Each instance is cached per type-pair via a private generic nested
/// class, so the static-field initializer runs once and every call to
/// <see cref="DictionaryFor{TKey, TValue}"/> / <see cref="HashSetFor{T}"/>
/// returns the same reference with zero allocation.
/// <para>
/// <strong>Mutation contract:</strong> consumers must treat these
/// instances as read-only. Use them as default sentinels for
/// "no entries yet" fields that get *reassigned* on first real
/// population — never as starting points for in-place
/// <see cref="Dictionary{TKey, TValue}.Add"/> / <see cref="HashSet{T}.Add"/>.
/// Mutating the singleton corrupts every other consumer of the same
/// type pair.
/// </para>
/// </remarks>
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

    /// <summary>Per type-pair singleton holder for the empty dictionary.</summary>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <typeparam name="TValue">Value type.</typeparam>
    private static class EmptyDictionaryHolder<TKey, TValue>
        where TKey : notnull
    {
        /// <summary>The shared empty dictionary instance.</summary>
        public static readonly Dictionary<TKey, TValue> Instance = [];
    }

    /// <summary>Per element-type singleton holder for the empty set.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    private static class EmptyHashSetHolder<T>
    {
        /// <summary>The shared empty set instance.</summary>
        public static readonly HashSet<T> Instance = [];
    }
}
