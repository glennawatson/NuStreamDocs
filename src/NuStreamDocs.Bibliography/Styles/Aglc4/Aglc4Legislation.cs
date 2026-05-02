// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>AGLC4 formatter for <see cref="EntryType.Legislation"/>.</summary>
/// <remarks>Form: <c>*Title (Year)* (Jurisdiction)</c>. Year is part of the italicized title in AGLC4.</remarks>
internal static class Aglc4Legislation
{
    /// <summary>Writes the citation per AGLC4 legislation rules directly to <paramref name="writer"/>.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Write(CitationEntry entry, IBufferWriter<byte> writer)
    {
        Aglc4Writer.WriteBytes("*"u8, writer);
        Aglc4Writer.WriteString(entry.Title, writer);
        Aglc4Writer.WriteBytes("*"u8, writer);

        if (entry.Jurisdiction.Length is 0)
        {
            return;
        }

        Aglc4Writer.WriteBytes(" ("u8, writer);
        Aglc4Writer.WriteString(entry.Jurisdiction, writer);
        Aglc4Writer.WriteBytes(")"u8, writer);
    }
}
