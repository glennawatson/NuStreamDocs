// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>AGLC4 formatter for <see cref="EntryType.Webpage"/>.</summary>
/// <remarks>Form: <c>Author, 'Title', *Site Name* (Web Page, Year) &lt;URL&gt;</c>.</remarks>
internal static class Aglc4Webpages
{
    /// <summary>Writes the citation per AGLC4 web-page rules directly to <paramref name="writer"/>.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Write(CitationEntry entry, IBufferWriter<byte> writer)
    {
        if (entry.Authors.Length > 0)
        {
            Aglc4Writer.WriteAuthors(entry.Authors, writer);
            Aglc4Writer.WriteBytes(", "u8, writer);
        }

        Aglc4Writer.WriteBytes("'"u8, writer);
        Aglc4Writer.WriteString(entry.Title, writer);
        Aglc4Writer.WriteBytes("'"u8, writer);

        if (entry.ContainerTitle.Length > 0)
        {
            Aglc4Writer.WriteBytes(", *"u8, writer);
            Aglc4Writer.WriteString(entry.ContainerTitle, writer);
            Aglc4Writer.WriteBytes("*"u8, writer);
        }

        if (entry.Year is 0)
        {
            Aglc4Writer.WriteBytes(" (Web Page)"u8, writer);
        }
        else
        {
            Aglc4Writer.WriteBytes(" (Web Page, "u8, writer);
            Aglc4Writer.WriteInt(entry.Year, writer);
            Aglc4Writer.WriteBytes(")"u8, writer);
        }

        if (entry.Url.Length is 0)
        {
            return;
        }

        Aglc4Writer.WriteBytes(" <"u8, writer);
        Aglc4Writer.WriteString(entry.Url, writer);
        Aglc4Writer.WriteBytes(">"u8, writer);
    }
}
