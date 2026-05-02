// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>AGLC4 formatter for <see cref="EntryType.Treaty"/>.</summary>
/// <remarks>Simplified form: <c>*Title* (Year)</c>; full AGLC4 treaty form (signed/entered-into-force dates, parties) lives in <see cref="CitationEntry.Note"/> when present.</remarks>
internal static class Aglc4Treaties
{
    /// <summary>Writes the citation per AGLC4 treaty rules directly to <paramref name="writer"/>.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Write(CitationEntry entry, IBufferWriter<byte> writer)
    {
        Aglc4Writer.WriteBytes("*"u8, writer);
        Aglc4Writer.WriteString(entry.Title, writer);
        Aglc4Writer.WriteBytes("*"u8, writer);

        Aglc4Writer.WriteParenthesizedYear(entry.Year, writer);

        if (entry.Note.Length is 0)
        {
            return;
        }

        Aglc4Writer.WriteBytes(", "u8, writer);
        Aglc4Writer.WriteString(entry.Note, writer);
    }
}
