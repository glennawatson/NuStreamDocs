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
/// only needs to be settled by <see cref="Plugins.IDocPlugin.OnFinaliseAsync"/>
/// time.
/// <para>
/// Last write wins: if two pages register the same ID, the later one
/// shadows the earlier. Plugins should namespace their IDs (e.g.
/// <c>api:System.String</c>, <c>cite:rfc-9110</c>) to avoid collisions.
/// </para>
/// </remarks>
public sealed class AutorefsRegistry
{
    /// <summary>Anchor index: ID → page-relative URL plus optional <c>#fragment</c>.</summary>
    private readonly ConcurrentDictionary<string, string> _anchors = new(StringComparer.Ordinal);

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

        _anchors[id] = string.IsNullOrEmpty(fragment)
            ? pageRelativeUrl
            : pageRelativeUrl + "#" + fragment;
    }

    /// <summary>Resolves an ID to its full URL.</summary>
    /// <param name="id">Reference ID.</param>
    /// <param name="url">Resolved URL on success.</param>
    /// <returns>True when the ID was registered.</returns>
    public bool TryResolve(string id, out string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _anchors.TryGetValue(id, out url!);
    }

    /// <summary>Drops every registered entry. Used between builds in watcher mode.</summary>
    public void Clear() => _anchors.Clear();

    /// <summary>Snapshots the registry into a right-sized <c>(id, url)</c> array. Order is unspecified.</summary>
    /// <returns>A fresh snapshot array.</returns>
    public (string Id, string Url)[] Snapshot()
    {
        KeyValuePair<string, string>[] kvs = [.. _anchors];
        var result = new (string Id, string Url)[kvs.Length];
        for (var i = 0; i < kvs.Length; i++)
        {
            result[i] = (kvs[i].Key, kvs[i].Value);
        }

        return result;
    }
}
