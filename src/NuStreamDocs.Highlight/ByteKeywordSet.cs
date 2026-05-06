// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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
    /// <param name="firstByteSet">First-byte dispatch set covering every entry's leading byte.</param>
    private ByteKeywordSet(byte[][][] byLength, bool ignoreCase, SearchValues<byte> firstByteSet)
    {
        _byLength = byLength;
        _ignoreCase = ignoreCase;
        FirstByteSet = firstByteSet;
    }

    /// <summary>Gets the auto-derived first-byte dispatch set covering every keyword's leading byte (and the case-flipped variant for case-insensitive sets).</summary>
    /// <remarks>
    /// Computed once at construction by walking every entry. Use this in place of a hand-curated
    /// <c>SearchValues.Create("…"u8)</c> when wiring a keyword rule's <c>FirstBytes</c> dispatch —
    /// <see cref="LexerRule.FirstBytes"/> on the matching rule can be set straight to this property.
    /// </remarks>
    public SearchValues<byte> FirstByteSet { get; }

    /// <summary>Builds a case-sensitive set from the supplied UTF-8 keywords. Pass <c>[.. "name"u8]</c> or <c>[.. "name"u8]</c> per entry.</summary>
    /// <param name="keywords">Keyword bytes; each entry must be non-empty.</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet Create(params byte[][] keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        return Build(keywords, ignoreCase: false);
    }

    /// <summary>Builds a case-insensitive set; entries must already be lowercase ASCII.</summary>
    /// <param name="lowercaseKeywords">Lowercase UTF-8 keyword bytes.</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet CreateIgnoreCase(params byte[][] lowercaseKeywords)
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
    /// <param name="keywords">Keyword bytes.</param>
    /// <param name="ignoreCase">Whether to use ASCII case-fold compare.</param>
    /// <returns>Built set.</returns>
    private static ByteKeywordSet Build(byte[][] keywords, bool ignoreCase)
    {
        if (keywords.Length is 0)
        {
            return new(new byte[1][][], ignoreCase, SearchValues.Create(ReadOnlySpan<byte>.Empty));
        }

        var maxLen = 0;
        for (var i = 0; i < keywords.Length; i++)
        {
            var kw = keywords[i];
            if (kw is null or [])
            {
                throw new ArgumentException("Keyword bytes must be non-null and non-empty.", nameof(keywords));
            }

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
            byLength[kw.Length][cursors[kw.Length]++] = kw;
        }

        return new(byLength, ignoreCase, BuildFirstByteSet(keywords, ignoreCase));
    }

    /// <summary>Builds a <see cref="SearchValues{T}"/> covering every <paramref name="keywords"/> entry's leading byte; for case-insensitive sets the case-flipped variant is included too.</summary>
    /// <param name="keywords">Keyword bytes.</param>
    /// <param name="ignoreCase">Whether to include the case-flipped variant of each ASCII-letter first byte.</param>
    /// <returns>First-byte dispatch set.</returns>
    private static SearchValues<byte> BuildFirstByteSet(byte[][] keywords, bool ignoreCase)
    {
        const int AsciiByteCount = 256;
        Span<bool> seen = stackalloc bool[AsciiByteCount];
        for (var i = 0; i < keywords.Length; i++)
        {
            var first = keywords[i][0];
            seen[first] = true;
            if (ignoreCase)
            {
                seen[CaseFlip(first)] = true;
            }
        }

        return MaterializeFlags(seen);
    }

    /// <summary>Returns the case-flipped variant for ASCII letters; non-letters pass through unchanged.</summary>
    /// <param name="b">Byte to flip.</param>
    /// <returns>Flipped byte (or original).</returns>
    private static byte CaseFlip(byte b) => b switch
    {
        >= (byte)'a' and <= (byte)'z' => (byte)(b & ~AsciiByteHelpers.AsciiCaseBit),
        >= (byte)'A' and <= (byte)'Z' => (byte)(b | AsciiByteHelpers.AsciiCaseBit),
        _ => b
    };

    /// <summary>Builds a <see cref="SearchValues{T}"/> from a 256-slot bool flag table.</summary>
    /// <param name="seen">Per-byte flag array.</param>
    /// <returns>Search values covering every set byte.</returns>
    private static SearchValues<byte> MaterializeFlags(ReadOnlySpan<bool> seen)
    {
        var distinctCount = 0;
        for (var b = 0; b < seen.Length; b++)
        {
            if (seen[b])
            {
                distinctCount++;
            }
        }

        var result = new byte[distinctCount];
        var idx = 0;
        for (var b = 0; b < seen.Length; b++)
        {
            if (seen[b])
            {
                result[idx++] = (byte)b;
            }
        }

        return SearchValues.Create(result);
    }
}
