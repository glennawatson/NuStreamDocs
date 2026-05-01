// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// ASCII word-boundary helpers shared by byte-level markdown rewriters.
/// </summary>
public static class AsciiWordBoundary
{
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
}
