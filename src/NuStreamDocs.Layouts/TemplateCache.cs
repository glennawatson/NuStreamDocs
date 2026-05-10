// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Layouts;

/// <summary>Per-build cache of parsed layout templates keyed by UTF-8 template name.</summary>
internal sealed class TemplateCache
{
    /// <summary>Initial bucket count.</summary>
    private const int InitialCapacity = 8;

    /// <summary>Backing store keyed by UTF-8 template name.</summary>
    private readonly Dictionary<byte[], TemplateEntry> _entries = new(InitialCapacity, ByteArrayComparer.Instance);

    /// <summary>Guards <see cref="_entries"/> against concurrent post-render workers.</summary>
    private readonly Lock _gate = new();

    /// <summary>Gets the current cached-entry count.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Looks up a cached entry by template name.</summary>
    /// <param name="templateName">UTF-8 template name.</param>
    /// <param name="entry">Cached entry on hit; default on miss.</param>
    /// <returns>True when cached.</returns>
    public bool TryGet(ReadOnlySpan<byte> templateName, out TemplateEntry entry)
    {
        lock (_gate)
        {
            return _entries.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(templateName, out entry!);
        }
    }

    /// <summary>Inserts <paramref name="entry"/> under <paramref name="templateName"/> if absent.</summary>
    /// <param name="templateName">UTF-8 template name; ownership transfers to the cache.</param>
    /// <param name="entry">Entry to associate with the name.</param>
    public void Add(byte[] templateName, TemplateEntry entry)
    {
        lock (_gate)
        {
            _entries.TryAdd(templateName, entry);
        }
    }

    /// <summary>Empties the cache.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
