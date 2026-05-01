// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Snippets;

/// <summary>Single-byte UTF-8 writer used by <see cref="SnippetsRewriter"/>.</summary>
internal static class SnippetsByteWriter
{
    /// <summary>Writes <paramref name="b"/> as a single UTF-8 byte to <paramref name="writer"/>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="b">Byte to write.</param>
    public static void WriteOne(IBufferWriter<byte> writer, byte b)
    {
        var dst = writer.GetSpan(1);
        dst[0] = b;
        writer.Advance(1);
    }
}
