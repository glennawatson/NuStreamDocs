// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>Helpers for assembling <see cref="NavEntry"/> arrays.</summary>
public static class NavBuilder
{
    /// <summary>Returns a right-sized <see cref="NavEntry"/> array from <paramref name="buffer"/> populated up to <paramref name="count"/>.</summary>
    /// <param name="buffer">Working buffer.</param>
    /// <param name="count">Number of valid entries at the start of <paramref name="buffer"/>.</param>
    /// <returns>A right-sized array; empty when <paramref name="count"/> is 0.</returns>
    public static NavEntry[] ToArray(NavEntry[] buffer, int count)
    {
        if (count == 0)
        {
            return [];
        }

        var result = new NavEntry[count];
        Array.Copy(buffer, result, count);
        return result;
    }
}
