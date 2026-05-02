// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Thread-safe map of cross-document reference IDs to the page URL +
/// fragment that owns them.
/// </summary>
/// <remarks>
/// Built up during the parallel render pass: heading scanners and
/// other plugins (e.g. <c>NuStreamDocs.CSharpApiGenerator</c>) call
/// <see cref="Register"/> from any worker. Resolution happens after
/// the pass when <see cref="AutorefsPlugin"/> rewrites
/// <c>@autoref:ID</c> markers in the emitted HTML, so the registry
/// only needs to be settled by <see cref="Plugins.IDocPlugin.OnFinalizeAsync"/>
/// time.
/// <para>
/// Last write wins: if two pages register the same ID, the later one
/// shadows the earlier. Plugins should namespace their IDs (e.g.
/// <c>api:System.String</c>, <c>cite:rfc-9110</c>) to avoid collisions.
/// </para>
/// <para>
/// Storage is split into <c>(pageUrl, fragment)</c> pairs rather than
/// a pre-composed <c>page#fragment</c> string. The heading scanner is
/// the dominant caller (~10 headings per page × thousands of pages),
/// and clustering by page URL means the dictionary holds many entries
/// that share the same <see cref="string"/> reference for their page —
/// no per-call concat allocation. Composition only happens on
/// <see cref="TryResolve"/> / <see cref="Snapshot"/>, which fire orders
/// of magnitude less often.
/// </para>
/// </remarks>
public sealed class AutorefsRegistry
{
    /// <summary>Default concurrency level when the caller does not pass a capacity hint.</summary>
    /// <remarks>
    /// Matches <see cref="ConcurrentDictionary{TKey, TValue}"/>'s default
    /// (number of cores). Kept explicit so the capacity overload below
    /// reuses the same value rather than re-deriving it.
    /// </remarks>
    private static readonly int DefaultConcurrencyLevel = Environment.ProcessorCount;

    /// <summary>Anchor index: ID → page URL + optional fragment.</summary>
    private readonly ConcurrentDictionary<string, Anchor> _anchors;

    /// <summary>Initializes a new instance of the <see cref="AutorefsRegistry"/> class with the default capacity.</summary>
    public AutorefsRegistry()
    {
        _anchors = new(StringComparer.Ordinal);
    }

    /// <summary>Initializes a new instance of the <see cref="AutorefsRegistry"/> class pre-sized for <paramref name="initialCapacity"/> entries.</summary>
    /// <param name="initialCapacity">Expected entry count — pass a hint when registering tens of thousands of headings to avoid bucket-resize churn.</param>
    public AutorefsRegistry(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
        _anchors = new(DefaultConcurrencyLevel, initialCapacity, StringComparer.Ordinal);
    }

    /// <summary>Gets the current entry count.</summary>
    public int Count => _anchors.Count;

    /// <summary>Registers an ID against a page URL and fragment.</summary>
    /// <param name="id">Reference ID.</param>
    /// <param name="pageRelativeUrl">Page-relative URL, forward-slashed.</param>
    /// <param name="fragment">Anchor fragment without the leading <c>#</c>; may be null/empty for whole-page references.</param>
    public void Register(string id, string pageRelativeUrl, string? fragment)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(pageRelativeUrl);

        // Normalize empty fragments to null so downstream composition
        // can branch on null alone, not "" vs null.
        var normalizedFragment = string.IsNullOrEmpty(fragment) ? null : fragment;
        _anchors[id] = new(pageRelativeUrl, normalizedFragment);
    }

    /// <summary>Resolves an ID to its full URL.</summary>
    /// <param name="id">Reference ID.</param>
    /// <param name="url">Resolved URL on success.</param>
    /// <returns>True when the ID was registered.</returns>
    public bool TryResolve(string id, out string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (!_anchors.TryGetValue(id, out var anchor))
        {
            url = string.Empty;
            return false;
        }

        url = anchor.Compose();
        return true;
    }

    /// <summary>Drops every registered entry. Used between builds in watcher mode.</summary>
    public void Clear() => _anchors.Clear();

    /// <summary>Snapshots the registry into a right-sized <c>(id, url)</c> array. Order is unspecified.</summary>
    /// <returns>A fresh snapshot array.</returns>
    public (string Id, string Url)[] Snapshot()
    {
        KeyValuePair<string, Anchor>[] kvs = [.. _anchors];
        var result = new (string Id, string Url)[kvs.Length];
        for (var i = 0; i < kvs.Length; i++)
        {
            result[i] = (kvs[i].Key, kvs[i].Value.Compose());
        }

        return result;
    }

    /// <summary>Stored anchor — page URL + optional fragment, composed only on read.</summary>
    /// <param name="PageUrl">Page-relative URL.</param>
    /// <param name="Fragment">Optional anchor fragment without the leading <c>#</c>.</param>
    private readonly record struct Anchor(string PageUrl, string? Fragment)
    {
        /// <summary>Composes the stored URL into <c>page</c> or <c>page#fragment</c> form.</summary>
        /// <returns>Composed URL.</returns>
        public string Compose() => Fragment is null ? PageUrl : PageUrl + "#" + Fragment;
    }
}
