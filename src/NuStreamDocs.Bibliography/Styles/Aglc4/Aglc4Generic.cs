// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>Catch-all AGLC4 formatter for entry types without a dedicated handler — falls back to the book shape.</summary>
internal static class Aglc4Generic
{
    /// <summary>Writes the citation using the book-shape fallback directly to <paramref name="writer"/>.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Write(CitationEntry entry, IBufferWriter<byte> writer) => Aglc4Books.Write(entry, writer);
}
