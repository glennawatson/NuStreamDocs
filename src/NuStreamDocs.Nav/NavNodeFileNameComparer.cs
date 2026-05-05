// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Singleton comparer that orders <see cref="NavNode"/>s by explicit <c>Order:</c> first then file name.</summary>
internal sealed class NavNodeFileNameComparer : IComparer<NavNode>
{
    /// <summary>Gets the shared instance to avoid per-sort allocations.</summary>
    public static NavNodeFileNameComparer Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(NavNode? x, NavNode? y)
    {
        var xOrder = x?.Order ?? int.MaxValue;
        var yOrder = y?.Order ?? int.MaxValue;
        return xOrder != yOrder
            ? xOrder.CompareTo(yOrder)
            : string.Compare(x?.RelativePath, y?.RelativePath, StringComparison.OrdinalIgnoreCase);
    }
}
