// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog.Common;

/// <summary>Shared slugification helper for tag/category archive filenames.</summary>
internal static class BlogSlugifier
{
    /// <summary>Difference between an ASCII uppercase and lowercase letter.</summary>
    private const byte AsciiCaseShift = 32;

    /// <summary>Slugifies <paramref name="value"/>: lowercased ASCII alphanumerics plus <c>-</c>/<c>_</c>; everything else collapses or drops.</summary>
    /// <param name="value">Source UTF-8 bytes.</param>
    /// <param name="fallback">Returned when nothing slug-safe survives.</param>
    /// <returns>Slug bytes.</returns>
    public static byte[] Slugify(ReadOnlySpan<byte> value, ReadOnlySpan<byte> fallback)
    {
        var dst = new byte[value.Length];
        var written = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var mapped = MapByte(value[i]);
            if (mapped != 0)
            {
                dst[written++] = mapped;
            }
        }

        if (written == 0)
        {
            return [.. fallback];
        }

        if (written != dst.Length)
        {
            Array.Resize(ref dst, written);
        }

        return dst;
    }

    /// <summary>Maps one byte to its slug equivalent or <c>0</c> when it should be dropped.</summary>
    /// <param name="b">Source byte.</param>
    /// <returns>Slug byte or NUL.</returns>
    private static byte MapByte(byte b)
    {
        if (IsLowerAlphanumeric(b) || b is (byte)'-' or (byte)'_')
        {
            return b;
        }

        if (b is >= (byte)'A' and <= (byte)'Z')
        {
            return (byte)(b + AsciiCaseShift);
        }

        if (b is (byte)' ' or (byte)'/')
        {
            return (byte)'-';
        }

        return (byte)0;
    }

    /// <summary>True for ASCII <c>a–z</c> or <c>0–9</c>.</summary>
    /// <param name="b">Byte.</param>
    /// <returns>True when slug-safe without case translation.</returns>
    private static bool IsLowerAlphanumeric(byte b) =>
        b is >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9';
}
