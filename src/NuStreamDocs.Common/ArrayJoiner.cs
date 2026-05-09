// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>Helpers for joining two arrays of the same element type.</summary>
public static class ArrayJoiner
{
    /// <summary>Concatenates <paramref name="head"/> and <paramref name="tail"/> into a single array; returns the non-empty side directly when one is empty.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="head">Existing entries.</param>
    /// <param name="tail">Entries to append.</param>
    /// <returns>The combined array.</returns>
    public static T[] Concat<T>(T[] head, T[] tail)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(tail);

        if (head.Length is 0)
        {
            return tail;
        }

        if (tail.Length is 0)
        {
            return head;
        }

        var merged = new T[head.Length + tail.Length];
        Array.Copy(head, 0, merged, 0, head.Length);
        Array.Copy(tail, 0, merged, head.Length, tail.Length);
        return merged;
    }
}
