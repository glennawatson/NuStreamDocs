// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Byte-level UTF-8 helpers shared across the privacy byte scanners
/// — case-insensitive ASCII compares, attribute / word-boundary
/// detection, whitespace skip, encoded-string emit.
/// </summary>
internal static class ByteHelpers
{
    /// <summary>ASCII bit that distinguishes uppercase letters from lowercase letters; OR-ing with this folds case.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Returns true when <paramref name="b"/> contributes to an ASCII identifier (letter / digit / underscore).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for identifier bytes.</returns>
    public static bool IsAsciiIdentifierByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'_';

    /// <summary>Returns true when <paramref name="b"/> is ASCII whitespace.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for whitespace.</returns>
    public static bool IsAsciiWhitespace(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    /// <summary>Returns true when offset <paramref name="offset"/> in <paramref name="source"/> is at an ASCII <c>\b</c> word boundary.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <returns>True when the byte before <paramref name="offset"/> is not an identifier byte.</returns>
    public static bool IsWordBoundary(ReadOnlySpan<byte> source, int offset) =>
        offset is 0 || !IsAsciiIdentifierByte(source[offset - 1]);

    /// <summary>Skips ASCII whitespace from <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Starting offset.</param>
    /// <returns>Offset of the first non-whitespace byte.</returns>
    public static int SkipWhitespace(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && IsAsciiWhitespace(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Case-insensitive ASCII byte-prefix match at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate offset.</param>
    /// <param name="lowerCase">Lowercase ASCII bytes to compare against.</param>
    /// <returns>True when the bytes at <paramref name="offset"/> equal <paramref name="lowerCase"/> ignoring ASCII case.</returns>
    public static bool StartsWithIgnoreAsciiCase(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> lowerCase)
    {
        if (offset + lowerCase.Length > source.Length)
        {
            return false;
        }

        for (var i = 0; i < lowerCase.Length; i++)
        {
            if ((source[offset + i] | AsciiCaseBit) != lowerCase[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Case-insensitive ASCII equality test over the full spans (lengths must match).</summary>
    /// <param name="a">Span to test.</param>
    /// <param name="b">Lowercase ASCII span to compare against.</param>
    /// <returns>True when equal ignoring ASCII case.</returns>
    public static bool EqualsIgnoreAsciiCase(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if ((a[i] | AsciiCaseBit) != b[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Encodes <paramref name="value"/> into <paramref name="sink"/> as UTF-8 with no intermediate string.</summary>
    /// <param name="value">Source string.</param>
    /// <param name="sink">UTF-8 sink.</param>
    public static void EncodeStringInto(string value, IBufferWriter<byte> sink)
    {
        if (value.Length is 0)
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var dst = sink.GetSpan(max);
        var written = Encoding.UTF8.GetBytes(value, dst);
        sink.Advance(written);
    }
}
