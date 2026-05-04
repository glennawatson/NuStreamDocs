// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>Singleton comparer that orders <see cref="NavNode"/>s by title.</summary>
/// <remarks>Compares the UTF-8 title bytes via case-insensitive ASCII fold — sufficient for the
/// repo's English-language titles and faster than decoding to <see cref="string"/> per compare.</remarks>
internal sealed class NavNodeTitleComparer : IComparer<NavNode>
{
    /// <summary>Gets the shared instance to avoid per-sort allocations.</summary>
    public static NavNodeTitleComparer Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(NavNode? x, NavNode? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        if (x.Order != y.Order)
        {
            return x.Order.CompareTo(y.Order);
        }

        return AsciiByteHelpers.CompareIgnoreAsciiCase(x.Title, y.Title);
    }
}
