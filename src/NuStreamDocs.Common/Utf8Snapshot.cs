// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Common;

/// <summary>UTF-8 byte-array snapshot decode helpers used by string-shaped public adapters over byte-shaped registries.</summary>
public static class Utf8Snapshot
{
    /// <summary>Decodes every entry of <paramref name="bytes"/> from UTF-8 to a fresh <see cref="string"/> array.</summary>
    /// <param name="bytes">UTF-8 byte arrays.</param>
    /// <returns>Right-sized decoded string array.</returns>
    public static string[] Decode(byte[][] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var result = new string[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            result[i] = Encoding.UTF8.GetString(bytes[i]);
        }

        return result;
    }
}
