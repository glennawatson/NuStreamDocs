// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Highlight.Languages.Common.Builders;

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

    /// <summary>Builds a case-sensitive set from a single UTF-8 byte literal whose entries are separated by ASCII space or tab.</summary>
    /// <param name="spaceSeparated">Whitespace-delimited UTF-8 keyword bytes (e.g. <c>"if else for"u8</c>).</param>
    /// <returns>Built set.</returns>
    /// <remarks>
    /// Empty runs (consecutive whitespace) are skipped silently; only ASCII space (<c>0x20</c>) and tab (<c>0x09</c>) act as separators.
    /// </remarks>
    public static ByteKeywordSet CreateFromSpaceSeparated(ReadOnlySpan<byte> spaceSeparated) =>
        Build(SplitSpaceSeparated(spaceSeparated), ignoreCase: false);

    /// <summary>Builds a case-sensitive set from two space-delimited UTF-8 chunks (the second is appended after a synthetic separator).</summary>
    /// <param name="spaceSeparatedFirst">First chunk of whitespace-delimited UTF-8 keyword bytes.</param>
    /// <param name="spaceSeparatedSecond">Second chunk of whitespace-delimited UTF-8 keyword bytes.</param>
    /// <returns>Built set.</returns>
    /// <remarks>
    /// Provided so very large keyword lists can be authored across multiple <c>"..."u8</c> literals
    /// without exceeding the S103 200-character line cap.
    /// </remarks>
    public static ByteKeywordSet CreateFromSpaceSeparated(ReadOnlySpan<byte> spaceSeparatedFirst, ReadOnlySpan<byte> spaceSeparatedSecond) =>
        Build(SplitTwoChunks(spaceSeparatedFirst, spaceSeparatedSecond), ignoreCase: false);

    /// <summary>Builds a case-sensitive set from three space-delimited UTF-8 chunks (each is appended after a synthetic separator).</summary>
    /// <param name="spaceSeparatedFirst">First chunk.</param>
    /// <param name="spaceSeparatedSecond">Second chunk.</param>
    /// <param name="spaceSeparatedThird">Third chunk.</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet CreateFromSpaceSeparated(ReadOnlySpan<byte> spaceSeparatedFirst, ReadOnlySpan<byte> spaceSeparatedSecond, ReadOnlySpan<byte> spaceSeparatedThird) =>
        Build(SplitThreeChunks(spaceSeparatedFirst, spaceSeparatedSecond, spaceSeparatedThird), ignoreCase: false);

    /// <summary>Builds a case-sensitive set from four space-delimited UTF-8 chunks.</summary>
    /// <param name="spaceSeparatedFirst">First chunk.</param>
    /// <param name="spaceSeparatedSecond">Second chunk.</param>
    /// <param name="spaceSeparatedThird">Third chunk.</param>
    /// <param name="spaceSeparatedFourth">Fourth chunk.</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet CreateFromSpaceSeparated(
        ReadOnlySpan<byte> spaceSeparatedFirst,
        ReadOnlySpan<byte> spaceSeparatedSecond,
        ReadOnlySpan<byte> spaceSeparatedThird,
        ReadOnlySpan<byte> spaceSeparatedFourth) =>
        Build(SplitFourChunks(spaceSeparatedFirst, spaceSeparatedSecond, spaceSeparatedThird, spaceSeparatedFourth), ignoreCase: false);

    /// <summary>Builds a case-insensitive set from a single UTF-8 byte literal whose entries are separated by ASCII space or tab; entries must already be lowercase ASCII.</summary>
    /// <param name="spaceSeparatedLowercase">Whitespace-delimited lowercase UTF-8 keyword bytes (e.g. <c>"select from where"u8</c>).</param>
    /// <returns>Built set.</returns>
    /// <remarks>
    /// Empty runs (consecutive whitespace) are skipped silently; only ASCII space (<c>0x20</c>) and tab (<c>0x09</c>) act as separators.
    /// </remarks>
    public static ByteKeywordSet CreateFromSpaceSeparatedIgnoreCase(ReadOnlySpan<byte> spaceSeparatedLowercase) =>
        Build(SplitSpaceSeparated(spaceSeparatedLowercase), ignoreCase: true);

    /// <summary>Builds a case-insensitive set from two space-delimited UTF-8 chunks (entries must already be lowercase ASCII).</summary>
    /// <param name="spaceSeparatedLowercaseFirst">First chunk of whitespace-delimited lowercase UTF-8 keyword bytes.</param>
    /// <param name="spaceSeparatedLowercaseSecond">Second chunk of whitespace-delimited lowercase UTF-8 keyword bytes.</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet CreateFromSpaceSeparatedIgnoreCase(ReadOnlySpan<byte> spaceSeparatedLowercaseFirst, ReadOnlySpan<byte> spaceSeparatedLowercaseSecond) =>
        Build(SplitTwoChunks(spaceSeparatedLowercaseFirst, spaceSeparatedLowercaseSecond), ignoreCase: true);

    /// <summary>Builds a case-insensitive set from three space-delimited UTF-8 chunks (entries must already be lowercase ASCII).</summary>
    /// <param name="spaceSeparatedLowercaseFirst">First chunk.</param>
    /// <param name="spaceSeparatedLowercaseSecond">Second chunk.</param>
    /// <param name="spaceSeparatedLowercaseThird">Third chunk.</param>
    /// <returns>Built set.</returns>
    public static ByteKeywordSet CreateFromSpaceSeparatedIgnoreCase(
        ReadOnlySpan<byte> spaceSeparatedLowercaseFirst,
        ReadOnlySpan<byte> spaceSeparatedLowercaseSecond,
        ReadOnlySpan<byte> spaceSeparatedLowercaseThird) =>
        Build(SplitThreeChunks(spaceSeparatedLowercaseFirst, spaceSeparatedLowercaseSecond, spaceSeparatedLowercaseThird), ignoreCase: true);

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

    /// <summary>Splits a UTF-8 byte span on ASCII space / tab, skipping empty runs.</summary>
    /// <param name="source">Source bytes.</param>
    /// <returns>Per-token byte arrays.</returns>
    private static byte[][] SplitSpaceSeparated(ReadOnlySpan<byte> source) =>
        WhitespaceSplitter.Split(source);

    /// <summary>Splits two UTF-8 byte chunks on ASCII space / tab into a single token table; equivalent to splitting their concatenation with a separator between.</summary>
    /// <param name="first">First source bytes.</param>
    /// <param name="second">Second source bytes.</param>
    /// <returns>Per-token byte arrays from both chunks.</returns>
    private static byte[][] SplitTwoChunks(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var firstTokens = WhitespaceSplitter.Split(first);
        var secondTokens = WhitespaceSplitter.Split(second);
        if (secondTokens.Length is 0)
        {
            return firstTokens;
        }

        if (firstTokens.Length is 0)
        {
            return secondTokens;
        }

        var combined = new byte[firstTokens.Length + secondTokens.Length][];
        Array.Copy(firstTokens, 0, combined, 0, firstTokens.Length);
        Array.Copy(secondTokens, 0, combined, firstTokens.Length, secondTokens.Length);
        return combined;
    }

    /// <summary>Splits three UTF-8 byte chunks on ASCII space / tab into a single token table.</summary>
    /// <param name="first">First source bytes.</param>
    /// <param name="second">Second source bytes.</param>
    /// <param name="third">Third source bytes.</param>
    /// <returns>Per-token byte arrays from all three chunks.</returns>
    private static byte[][] SplitThreeChunks(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third)
    {
        var firstTokens = WhitespaceSplitter.Split(first);
        var secondTokens = WhitespaceSplitter.Split(second);
        var thirdTokens = WhitespaceSplitter.Split(third);
        var total = firstTokens.Length + secondTokens.Length + thirdTokens.Length;
        if (total is 0)
        {
            return firstTokens;
        }

        var combined = new byte[total][];
        var cursor = 0;
        Array.Copy(firstTokens, 0, combined, cursor, firstTokens.Length);
        cursor += firstTokens.Length;
        Array.Copy(secondTokens, 0, combined, cursor, secondTokens.Length);
        cursor += secondTokens.Length;
        Array.Copy(thirdTokens, 0, combined, cursor, thirdTokens.Length);
        return combined;
    }

    /// <summary>Splits four UTF-8 byte chunks on ASCII space / tab into a single token table.</summary>
    /// <param name="first">First source bytes.</param>
    /// <param name="second">Second source bytes.</param>
    /// <param name="third">Third source bytes.</param>
    /// <param name="fourth">Fourth source bytes.</param>
    /// <returns>Per-token byte arrays from all four chunks.</returns>
    private static byte[][] SplitFourChunks(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third, ReadOnlySpan<byte> fourth)
    {
        var firstTokens = WhitespaceSplitter.Split(first);
        var secondTokens = WhitespaceSplitter.Split(second);
        var thirdTokens = WhitespaceSplitter.Split(third);
        var fourthTokens = WhitespaceSplitter.Split(fourth);
        var total = firstTokens.Length + secondTokens.Length + thirdTokens.Length + fourthTokens.Length;
        if (total is 0)
        {
            return firstTokens;
        }

        var combined = new byte[total][];
        var cursor = 0;
        Array.Copy(firstTokens, 0, combined, cursor, firstTokens.Length);
        cursor += firstTokens.Length;
        Array.Copy(secondTokens, 0, combined, cursor, secondTokens.Length);
        cursor += secondTokens.Length;
        Array.Copy(thirdTokens, 0, combined, cursor, thirdTokens.Length);
        cursor += thirdTokens.Length;
        Array.Copy(fourthTokens, 0, combined, cursor, fourthTokens.Length);
        return combined;
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
