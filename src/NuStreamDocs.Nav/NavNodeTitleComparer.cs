// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Singleton comparer that orders <see cref="NavNode"/>s by title.</summary>
internal sealed class NavNodeTitleComparer : IComparer<NavNode>
{
    /// <summary>Gets the shared instance to avoid per-sort allocations.</summary>
    public static NavNodeTitleComparer Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(NavNode? x, NavNode? y) =>
        string.Compare(x?.Title, y?.Title, StringComparison.OrdinalIgnoreCase);
}
