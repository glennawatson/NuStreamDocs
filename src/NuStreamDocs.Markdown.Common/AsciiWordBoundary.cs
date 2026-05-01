// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// ASCII word-boundary helpers shared by byte-level markdown rewriters.
/// </summary>
public static class AsciiWordBoundary
{
    /// <summary>ASCII bit that folds upper-case Latin letters to lower-case.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Returns true when <paramref name="offset"/> is at a word boundary on its left.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position to test.</param>
    /// <returns>True when the byte before <paramref name="offset"/> is not an ASCII word byte.</returns>
    public static bool IsBefore(ReadOnlySpan<byte> source, int offset) =>
        offset is 0 || !IsWordByte(source[offset - 1]);

    /// <summary>Returns true when <paramref name="offset"/> is at a word boundary on its right.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position to test.</param>
    /// <returns>True when the byte at <paramref name="offset"/> is not an ASCII word byte.</returns>
    public static bool IsAfter(ReadOnlySpan<byte> source, int offset) =>
        offset >= source.Length || !IsWordByte(source[offset]);

    /// <summary>Returns true when <paramref name="b"/> is an ASCII word byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for ASCII letters, digits, and underscore.</returns>
    public static bool IsWordByte(byte b) =>
        b is >= (byte)'0' and <= (byte)'9'
          or >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or (byte)'_';

    /// <summary>Returns true when <paramref name="token"/> matches exactly at <paramref name="offset"/> and is word-bounded on both sides.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate start offset.</param>
    /// <param name="token">ASCII/UTF-8 token to match.</param>
    /// <returns>True on a bounded exact match.</returns>
    public static bool TryMatchBounded(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> token) =>
        offset + token.Length <= source.Length
        && IsBefore(source, offset)
        && source.Slice(offset, token.Length).SequenceEqual(token)
        && IsAfter(source, offset + token.Length);

    /// <summary>Returns true when <paramref name="token"/> matches at <paramref name="offset"/> ignoring ASCII case and is word-bounded on both sides.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate start offset.</param>
    /// <param name="token">ASCII token to match case-insensitively.</param>
    /// <returns>True on a bounded ASCII-case-insensitive match.</returns>
    public static bool TryMatchBoundedIgnoreAsciiCase(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> token)
    {
        if (offset + token.Length > source.Length || !IsBefore(source, offset) || !IsAfter(source, offset + token.Length))
        {
            return false;
        }

        for (var i = 0; i < token.Length; i++)
        {
            var left = source[offset + i];
            var right = token[i];
            if (left == right)
            {
                continue;
            }

            if ((left | AsciiCaseBit) != (right | AsciiCaseBit))
            {
                return false;
            }
        }

        return true;
    }
}
