// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>AGLC4 formatter for journal / magazine / newspaper / generic articles.</summary>
/// <remarks>Form: <c>Author, 'Title' (Year) Volume *Journal* StartPage</c>.</remarks>
internal static class Aglc4Articles
{
    /// <summary>Writes the citation per AGLC4 article rules directly to <paramref name="writer"/>.</summary>
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

        Aglc4Writer.WriteParenthesizedYear(entry.Year, writer);

        if (entry.Volume.Length > 0)
        {
            Aglc4Writer.WriteBytes(" "u8, writer);
            Aglc4Writer.WriteString(entry.Volume, writer);
        }

        if (entry.ContainerTitle.Length > 0)
        {
            Aglc4Writer.WriteBytes(" *"u8, writer);
            Aglc4Writer.WriteString(entry.ContainerTitle, writer);
            Aglc4Writer.WriteBytes("*"u8, writer);
        }

        if (entry.Page.Length is 0)
        {
            return;
        }

        Aglc4Writer.WriteBytes(" "u8, writer);
        Aglc4Writer.WriteString(entry.Page, writer);
    }
}
