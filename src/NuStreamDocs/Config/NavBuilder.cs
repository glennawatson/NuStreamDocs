// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config;

/// <summary>
/// Allocation-conscious helpers for assembling <see cref="NavEntry"/> arrays.
/// </summary>
/// <remarks>
/// Keeps the per-entry path off the heap by writing directly into a
/// caller-rented buffer, then trimming in one move. Used by the config
/// reader and the awesome-nav plugin to avoid <c>List&lt;T&gt;</c>
/// resize churn on large sites.
/// </remarks>
internal static class NavBuilder
{
    /// <summary>
    /// Returns a properly sized <see cref="NavEntry"/> array from a
    /// <paramref name="buffer"/> partially populated up to
    /// <paramref name="count"/>.
    /// </summary>
    /// <param name="buffer">Working buffer, typically rented from
    /// <see cref="System.Buffers.ArrayPool{T}"/> by the caller.</param>
    /// <param name="count">Number of valid entries at the start of <paramref name="buffer"/>.</param>
    /// <returns>A right-sized array; empty when <paramref name="count"/> is 0.</returns>
    internal static NavEntry[] ToArray(NavEntry[] buffer, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (count == 0)
        {
            return [];
        }

        var result = new NavEntry[count];
        Array.Copy(buffer, result, count);
        return result;
    }
}
