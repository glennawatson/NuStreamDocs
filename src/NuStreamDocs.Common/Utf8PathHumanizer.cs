// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Common;

/// <summary>Converts path tokens (file or directory names) into human-readable titles emitted as UTF-8 bytes.</summary>
public static class Utf8PathHumanizer
{
    /// <summary>Lowest non-ASCII code point; chars at or above this are flushed via the UTF-8 encoder.</summary>
    private const int AsciiBoundary = 0x80;

    /// <summary>Lowercase-to-uppercase delta for ASCII letters.</summary>
    private const int AsciiCaseDelta = 'a' - 'A';

    /// <summary>Humanizes a file or directory token like <c>getting-started</c> into title text.</summary>
    /// <param name="name">Path token without separators.</param>
    /// <returns>UTF-8 title bytes.</returns>
    /// <remarks>
    /// Walks the source span byte-by-byte; ASCII-only transforms (separator → space, leading
    /// lowercase → uppercase) are written straight into a UTF-8 destination buffer, while non-ASCII
    /// runs (≥ 0x80) fall through to the standard encoder for the surrounding slice.
    /// </remarks>
    public static byte[] HumanizePathName(this ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
        {
            return [];
        }

        var maxBytes = Encoding.UTF8.GetMaxByteCount(name.Length);
        var rented = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            var written = WriteHumanized(name, rented);
            var result = new byte[written];
            rented.AsSpan(0, written).CopyTo(result);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Walks the source span and writes humanized UTF-8 bytes into <paramref name="destination"/>.</summary>
    /// <param name="name">Source path token.</param>
    /// <param name="destination">Pre-rented buffer sized for the worst-case UTF-8 expansion of <paramref name="name"/>.</param>
    /// <returns>Number of bytes written.</returns>
    private static int WriteHumanized(ReadOnlySpan<char> name, byte[] destination)
    {
        var written = 0;
        var makeUpper = true;
        var nonAsciiStart = -1;
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (current >= AsciiBoundary)
            {
                if (nonAsciiStart < 0)
                {
                    nonAsciiStart = i;
                }

                makeUpper = false;
                continue;
            }

            if (nonAsciiStart >= 0)
            {
                written += Encoding.UTF8.GetBytes(name[nonAsciiStart..i], destination.AsSpan(written));
                nonAsciiStart = -1;
            }

            written += WriteAsciiChar(current, destination.AsSpan(written), ref makeUpper);
        }

        if (nonAsciiStart >= 0)
        {
            written += Encoding.UTF8.GetBytes(name[nonAsciiStart..], destination.AsSpan(written));
        }

        return written;
    }

    /// <summary>Writes a single ASCII char to <paramref name="destination"/>, applying separator-to-space and lead-letter casing rules.</summary>
    /// <param name="current">ASCII char to emit.</param>
    /// <param name="destination">Slice of the output buffer at the current write offset.</param>
    /// <param name="makeUpper">Tracks whether the next ASCII letter should be upper-cased; updated in place.</param>
    /// <returns>Number of bytes written (always 1).</returns>
    private static int WriteAsciiChar(char current, Span<byte> destination, ref bool makeUpper)
    {
        if (current is '-' or '_')
        {
            destination[0] = (byte)' ';
            makeUpper = true;
            return 1;
        }

        destination[0] = makeUpper && current is >= 'a' and <= 'z'
            ? (byte)(current - AsciiCaseDelta)
            : (byte)current;
        makeUpper = current is ' ';
        return 1;
    }
}
