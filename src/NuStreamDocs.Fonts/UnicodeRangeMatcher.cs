// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Tracks which Unicode 256-codepoint blocks a body of text touches, and tests whether a CSS <c>unicode-range</c> overlaps any of them.</summary>
public static class UnicodeRangeMatcher
{
    /// <summary>Highest codepoint tracked (the BMP); higher codepoints aren't covered by the script subsets fonts ship.</summary>
    private const int MaxCodepoint = 0xFFFF;

    /// <summary>Codepoints per tracked block.</summary>
    private const int BlockSize = 256;

    /// <summary>Hex radix.</summary>
    private const int HexRadix = 16;

    /// <summary>Value offset for the hex letters <c>a-f</c> / <c>A-F</c>.</summary>
    private const int HexLetterOffset = 10;

    /// <summary>Gets the number of tracked blocks.</summary>
    public static int BlockCount => (MaxCodepoint / BlockSize) + 1;

    /// <summary>Creates a fresh block-bitset with block 0 (ASCII / Latin-1) already marked, since every page contains ASCII.</summary>
    /// <returns>The bitset.</returns>
    public static bool[] NewSeenBlocks()
    {
        var blocks = new bool[BlockCount];
        blocks[0] = true;
        return blocks;
    }

    /// <summary>Marks every block touched by the UTF-8 text in <paramref name="utf8"/>.</summary>
    /// <param name="utf8">UTF-8 text.</param>
    /// <param name="seenBlocks">Bitset to update.</param>
    public static void MarkSeen(ReadOnlySpan<byte> utf8, bool[] seenBlocks)
    {
        var i = 0;
        while (i < utf8.Length)
        {
            if (Rune.DecodeFromUtf8(utf8[i..], out var rune, out var consumed) != OperationStatus.Done)
            {
                i++;
                continue;
            }

            i += consumed;
            if (rune.Value <= MaxCodepoint)
            {
                seenBlocks[rune.Value / BlockSize] = true;
            }
        }
    }

    /// <summary>Tests whether the CSS <c>unicode-range</c> value <paramref name="unicodeRange"/> covers any block marked in <paramref name="seenBlocks"/>.</summary>
    /// <param name="unicodeRange">UTF-8 <c>unicode-range</c> value (e.g. <c>U+0000-00FF, U+0131, U+0400-045F</c>).</param>
    /// <param name="seenBlocks">Block bitset.</param>
    /// <returns><see langword="true"/> when at least one entry overlaps a seen block.</returns>
    public static bool Overlaps(ReadOnlySpan<byte> unicodeRange, bool[] seenBlocks)
    {
        var pos = 0;
        while (pos < unicodeRange.Length)
        {
            var commaRel = unicodeRange[pos..].IndexOf((byte)',');
            var entry = AsciiByteHelpers.TrimAsciiWhitespace(commaRel < 0
                ? unicodeRange[pos..]
                : unicodeRange.Slice(pos, commaRel));
            if (TryParseEntry(entry, out var lo, out var hi) && AnyBlockSeen(lo, hi, seenBlocks))
            {
                return true;
            }

            if (commaRel < 0)
            {
                break;
            }

            pos += commaRel + 1;
        }

        return false;
    }

    /// <summary>Returns whether any block in <c>[lo, hi]</c> is marked.</summary>
    /// <param name="lo">Inclusive low codepoint.</param>
    /// <param name="hi">Inclusive high codepoint.</param>
    /// <param name="seenBlocks">Block bitset.</param>
    /// <returns><see langword="true"/> when a block in the range is marked.</returns>
    private static bool AnyBlockSeen(int lo, int hi, bool[] seenBlocks)
    {
        var low = Math.Max(0, lo);
        var high = Math.Min(MaxCodepoint, hi);
        if (low > high)
        {
            return false;
        }

        for (var block = low / BlockSize; block <= high / BlockSize; block++)
        {
            if (seenBlocks[block])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses one <c>unicode-range</c> entry: <c>U+XXXX</c>, <c>U+XXXX-YYYY</c>, or a <c>?</c>-wildcard form like <c>U+00??</c>.</summary>
    /// <param name="entry">The trimmed entry bytes.</param>
    /// <param name="lo">Inclusive low codepoint.</param>
    /// <param name="hi">Inclusive high codepoint.</param>
    /// <returns><see langword="true"/> on a successful parse.</returns>
    private static bool TryParseEntry(ReadOnlySpan<byte> entry, out int lo, out int hi)
    {
        lo = 0;
        hi = 0;
        if (entry.Length < 3 || entry[0] is not ((byte)'U' or (byte)'u') || entry[1] != (byte)'+')
        {
            return false;
        }

        var body = entry[2..];
        var dash = body.IndexOf((byte)'-');
        if (dash < 0)
        {
            return TryParseHex(body, out lo, out hi);
        }

        return TryParseHex(body[..dash], out lo, out _) && TryParseHex(body[(dash + 1)..], out _, out hi);
    }

    /// <summary>Parses a hex string that may contain <c>?</c> wildcards into its inclusive low/high codepoints.</summary>
    /// <param name="hex">Hex digits, optionally with trailing <c>?</c> wildcards.</param>
    /// <param name="lo">Low codepoint (wildcards as 0).</param>
    /// <param name="hi">High codepoint (wildcards as F).</param>
    /// <returns><see langword="true"/> when every character is a hex digit or <c>?</c> and the string is non-empty.</returns>
    private static bool TryParseHex(ReadOnlySpan<byte> hex, out int lo, out int hi)
    {
        lo = 0;
        hi = 0;
        if (hex.Length is 0)
        {
            return false;
        }

        for (var i = 0; i < hex.Length; i++)
        {
            var c = hex[i];
            if (c == (byte)'?')
            {
                lo *= HexRadix;
                hi = (hi * HexRadix) + (HexRadix - 1);
                continue;
            }

            var digit = HexValue(c);
            if (digit < 0)
            {
                return false;
            }

            lo = (lo * HexRadix) + digit;
            hi = (hi * HexRadix) + digit;
        }

        return true;
    }

    /// <summary>Returns the value of a hex digit byte, or -1 when it isn't one.</summary>
    /// <param name="c">ASCII byte.</param>
    /// <returns>0-15, or -1.</returns>
    private static int HexValue(byte c) => c switch
    {
        >= (byte)'0' and <= (byte)'9' => c - (byte)'0',
        >= (byte)'a' and <= (byte)'f' => c - (byte)'a' + HexLetterOffset,
        >= (byte)'A' and <= (byte)'F' => c - (byte)'A' + HexLetterOffset,
        _ => -1
    };
}
