// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Common;

/// <summary>String / set conversion helpers for UTF-8 byte snapshots stored as <c>byte[][]</c>.</summary>
public static class ByteArrayCollectionExtensions
{
    /// <summary>Extension members for <c>byte[][]</c>.</summary>
    /// <param name="source">Value to operate on.</param>
    extension(byte[][] source)
    {
        /// <summary>Decodes every entry as UTF-8 into a <see cref="HashSet{T}"/> keyed by the supplied comparer.</summary>
        /// <param name="comparer">Equality comparer for the resulting strings.</param>
        /// <returns>A right-sized set populated with one decoded string per entry.</returns>
        public HashSet<string> ToStringSet(IEqualityComparer<string> comparer)
        {
            HashSet<string> set = [with(source.Length, comparer)];
            for (var i = 0; i < source.Length; i++)
            {
                _ = set.Add(Encoding.UTF8.GetString(source[i]));
            }

            return set;
        }

        /// <summary>Decodes every entry as UTF-8 into a fresh <see cref="string"/> array.</summary>
        /// <returns>One decoded string per entry.</returns>
        public string[] ToStringArray()
        {
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
}
