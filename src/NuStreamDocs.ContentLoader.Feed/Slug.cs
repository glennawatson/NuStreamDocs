// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.Feed;

/// <summary>Derives a URL-safe slug from arbitrary UTF-8 text.</summary>
internal static class Slug
{
    /// <summary>Gets the fallback slug used when the source produces nothing usable.</summary>
    private static ReadOnlySpan<byte> Fallback => "item"u8;

    /// <summary>Lowercases ASCII letters/digits in <paramref name="source"/> and collapses every other run into a single hyphen.</summary>
    /// <param name="source">Source bytes (a title, an id, …).</param>
    /// <returns>A non-empty slug.</returns>
    public static byte[] FromBytes(ReadOnlySpan<byte> source)
    {
        var buffer = new byte[source.Length + 1];
        var length = 0;
        var lastWasHyphen = true;
        for (var i = 0; i < source.Length; i++)
        {
            var b = source[i];
            if (AsciiByteHelpers.IsAsciiLetter(b) || AsciiByteHelpers.IsAsciiDigit(b))
            {
                buffer[length++] = AsciiByteHelpers.ToAsciiLowerByte(b);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                buffer[length++] = (byte)'-';
                lastWasHyphen = true;
            }
        }

        if (length > 0 && buffer[length - 1] == (byte)'-')
        {
            length--;
        }

        return length == 0 ? [.. Fallback] : buffer[..length];
    }
}
