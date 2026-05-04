// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Yaml;

/// <summary>Allocation-free integer parser for UTF-8 byte spans used by the front-matter readers.</summary>
internal static class Utf8IntParser
{
    /// <summary>Decimal radix used by the per-digit accumulator in <see cref="TryParseInt"/>.</summary>
    private const int DecimalRadix = 10;

    /// <summary>Parses an optional-sign decimal integer from <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 bytes; surrounding whitespace already trimmed.</param>
    /// <param name="value">The parsed integer on success.</param>
    /// <returns>True when every byte is a digit (with an optional leading <c>-</c>) and the result fits in an <see cref="int"/>.</returns>
    public static bool TryParseInt(ReadOnlySpan<byte> bytes, out int value)
    {
        value = 0;
        if (bytes.IsEmpty)
        {
            return false;
        }

        var i = 0;
        var negative = false;
        if (bytes[0] is (byte)'-')
        {
            negative = true;
            i = 1;
        }
        else if (bytes[0] is (byte)'+')
        {
            i = 1;
        }

        if (i >= bytes.Length)
        {
            return false;
        }

        long acc = 0;
        for (; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b is < (byte)'0' or > (byte)'9')
            {
                return false;
            }

            acc = (acc * DecimalRadix) + (b - (byte)'0');
            if (acc > int.MaxValue)
            {
                return false;
            }
        }

        value = negative ? -(int)acc : (int)acc;
        return true;
    }
}
