// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Shared body scanner for <c>:identifier:</c> shortcodes (emoji,
/// icon font shortcodes). The body is the byte run between the
/// surrounding colons.
/// </summary>
public static class ShortcodeScanner
{
    /// <summary>Scans a shortcode body starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">First-byte position of the body (just past the leading <c>:</c>).</param>
    /// <returns>Exclusive end of the body span.</returns>
    public static int ScanBody(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && IsBodyByte(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Returns true when <paramref name="b"/> is a permissible shortcode body byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for letters, digits, <c>_</c>, <c>+</c>, <c>-</c>, <c>.</c>.</returns>
    public static bool IsBodyByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'_' or (byte)'+' or (byte)'-' or (byte)'.';
}
