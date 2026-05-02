// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Small read-only ASCII keyword set keyed by UTF-8 bytes; the
/// per-token alternative to a <c>FrozenSet&lt;string&gt;</c>.
/// </summary>
/// <remarks>
/// Keywords are bucketed by length on construction; lookup probes the
/// bucket for the candidate's length and linear-scans the (typically
/// 1–10 entry) bucket. For the size of a language keyword set this
/// outperforms a generic hash set even with span lookup, because the
/// hash itself dominates compared to a bounded SequenceEqual.
/// <para>
/// Both case-sensitive and case-insensitive variants are supported.
/// The case-insensitive variant assumes ASCII letters and uses the
/// bit-5 fold trick from <see cref="AsciiByteHelpers"/> — every entry
/// must be supplied lowercase.
/// </para>
/// </remarks>
public sealed class ByteKeywordSet
{
    /// <summary>Empty bucket used as a sentinel for unused length slots.</summary>
    private static readonly byte[][] EmptyBucket = [];

    /// <summary>Length-indexed bucket table (<c>_byLength[len]</c>).</summary>
    private readonly byte[][][] _byLength;

    /// <summary>Whether <see cref="Contains"/> uses the ASCII case-fold compare.</summary>
    private readonly bool _ignoreCase;

    /// <summary>Initializes a new instance of the <see cref="ByteKeywordSet"/> class.</summary>
    /// <param name="byLength">Length-indexed bucket table.</param>
    /// <param name="ignoreCase">Whether to use ASCII case-fold compare.</param>
    private ByteKeywordSet(byte[][][] byLength, bool ignoreCase)
    {
        _byLength = byLength;
        _ignoreCase = ignoreCase;
    }

    /// <summary>Builds a case-sensitive set from the supplied ASCII string keywords.</summary>
    /// <param name="keywords">Keyword list.</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet Create(params string[] keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        return Build(keywords, ignoreCase: false);
    }

    /// <summary>Builds a case-insensitive set; entries must already be lowercase ASCII.</summary>
    /// <param name="lowercaseKeywords">Keyword list (lowercase).</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet CreateIgnoreCase(params string[] lowercaseKeywords)
    {
        ArgumentNullException.ThrowIfNull(lowercaseKeywords);
        return Build(lowercaseKeywords, ignoreCase: true);
    }

    /// <summary>Returns true when <paramref name="word"/> matches one of the registered keywords.</summary>
    /// <param name="word">Candidate word (UTF-8 bytes).</param>
    /// <returns>True on match.</returns>
    public bool Contains(ReadOnlySpan<byte> word)
    {
        if ((uint)word.Length >= (uint)_byLength.Length)
        {
            return false;
        }

        var bucket = _byLength[word.Length];
        for (var i = 0; i < bucket.Length; i++)
        {
            if (_ignoreCase ? AsciiByteHelpers.EqualsIgnoreAsciiCase(word, bucket[i]) : word.SequenceEqual(bucket[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds the length-indexed bucket table from the supplied keywords.</summary>
    /// <param name="keywords">Keyword strings.</param>
    /// <param name="ignoreCase">Whether to use ASCII case-fold compare.</param>
    /// <returns>Built set.</returns>
    private static ByteKeywordSet Build(string[] keywords, bool ignoreCase)
    {
        if (keywords.Length is 0)
        {
            return new(new byte[1][][], ignoreCase);
        }

        var maxLen = 0;
        for (var i = 0; i < keywords.Length; i++)
        {
            var kw = keywords[i];
            ArgumentException.ThrowIfNullOrEmpty(kw);
            if (kw.Length > maxLen)
            {
                maxLen = kw.Length;
            }
        }

        var counts = new int[maxLen + 1];
        for (var i = 0; i < keywords.Length; i++)
        {
            counts[keywords[i].Length]++;
        }

        var byLength = new byte[maxLen + 1][][];
        for (var len = 0; len <= maxLen; len++)
        {
            byLength[len] = counts[len] is 0 ? EmptyBucket : new byte[counts[len]][];
        }

        var cursors = new int[maxLen + 1];
        for (var i = 0; i < keywords.Length; i++)
        {
            var kw = keywords[i];
            byLength[kw.Length][cursors[kw.Length]++] = Encoding.UTF8.GetBytes(kw);
        }

        return new(byLength, ignoreCase);
    }
}
