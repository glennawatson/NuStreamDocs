// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>AGLC4 formatter for <see cref="EntryType.Thesis"/>.</summary>
/// <remarks>Form: <c>Author, '*Title*' (Note, Publisher, Year)</c>; <see cref="CitationEntry.Note"/> carries the thesis-type prefix when supplied.</remarks>
internal static class Aglc4Thesis
{
    /// <summary>Writes the citation per AGLC4 thesis rules directly to <paramref name="writer"/>.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Write(CitationEntry entry, IBufferWriter<byte> writer)
    {
        if (entry.Authors.Length > 0)
        {
            Aglc4Writer.WriteAuthors(entry.Authors, writer);
            Aglc4Writer.WriteBytes(", "u8, writer);
        }

        Aglc4Writer.WriteBytes("'*"u8, writer);
        Aglc4Writer.WriteString(entry.Title, writer);
        Aglc4Writer.WriteBytes("*'"u8, writer);

        WriteContextBlock(entry, writer);
    }

    /// <summary>Writes the trailing <c> (Note, Publisher, Year)</c> block, omitting empty pieces.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteContextBlock(CitationEntry entry, IBufferWriter<byte> writer)
    {
        var hasNote = entry.Note.Length > 0;
        var hasPublisher = entry.Publisher.Length > 0;
        var hasYear = entry.Year is not 0;
        if (!hasNote && !hasPublisher && !hasYear)
        {
            return;
        }

        Aglc4Writer.WriteBytes(" ("u8, writer);
        var emitted = false;
        if (hasNote)
        {
            Aglc4Writer.WriteString(entry.Note, writer);
            emitted = true;
        }

        if (hasPublisher)
        {
            if (emitted)
            {
                Aglc4Writer.WriteBytes(", "u8, writer);
            }

            Aglc4Writer.WriteString(entry.Publisher, writer);
            emitted = true;
        }

        if (hasYear)
        {
            if (emitted)
            {
                Aglc4Writer.WriteBytes(", "u8, writer);
            }

            Aglc4Writer.WriteInt(entry.Year, writer);
        }

        Aglc4Writer.WriteBytes(")"u8, writer);
    }
}
