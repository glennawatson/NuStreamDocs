// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>Generic helpers for joining two arrays of the same element type.</summary>
/// <remarks>
/// Used by option records' <c>AddXxx</c> helpers to append to an existing snapshot without
/// touching either input. Skips the alloc entirely when one side is empty by returning the
/// other side directly. Generic so it serves both byte-shaped storage (<c>byte[][]</c> via
/// <c>T = byte[]</c>) and string-shaped storage (<c>string[]</c> via <c>T = string</c>) — the
/// JIT specializes per element type.
/// </remarks>
public static class ArrayJoiner
{
    /// <summary>Concatenates <paramref name="head"/> and <paramref name="tail"/> into a single right-sized array, returning the non-empty side directly when one is empty.</summary>
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
