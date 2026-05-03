// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Common;

/// <summary>String / set conversion helpers for UTF-8 byte snapshots stored as <c>byte[][]</c>.</summary>
/// <remarks>
/// Plugins keep their option-record list fields as <c>byte[][]</c> (encode-once-at-construction)
/// but sometimes need a string-keyed lookup at run time — typically when matching against an API
/// that already produces strings (e.g. <see cref="Path.GetExtension(string)"/>). These helpers
/// consolidate the decode-once-at-build pattern so consumers only pass the source array.
/// </remarks>
public static class ByteArrayCollectionExtensions
{
    /// <summary>Decodes every entry as UTF-8 into a <see cref="HashSet{T}"/> keyed by the supplied comparer.</summary>
    /// <param name="source">UTF-8 entries.</param>
    /// <param name="comparer">Equality comparer for the resulting strings.</param>
    /// <returns>A right-sized set populated with one decoded string per entry.</returns>
    public static HashSet<string> ToStringSet(this byte[][] source, IEqualityComparer<string> comparer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);

        var set = new HashSet<string>(source.Length, comparer);
        for (var i = 0; i < source.Length; i++)
        {
            set.Add(Encoding.UTF8.GetString(source[i]));
        }

        return set;
    }

    /// <summary>Decodes every entry as UTF-8 into a fresh <see cref="string"/> array.</summary>
    /// <param name="source">UTF-8 entries.</param>
    /// <returns>One decoded string per entry.</returns>
    public static string[] ToStringArray(this byte[][] source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Length is 0)
        {
            return [];
        }

        var result = new string[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            result[i] = Encoding.UTF8.GetString(source[i]);
        }

        return result;
    }
}
