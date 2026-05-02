// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>AGLC4 formatter for <see cref="EntryType.LegalCase"/>.</summary>
/// <remarks>
/// Reported form: <c>*Case Name* Volume Series Page</c> (e.g.
/// <c>*Mabo v Queensland (No 2)* (1992) 175 CLR 1</c>). Falls through
/// to the medium-neutral citation when no law-report series is set,
/// then to the bare title with year when neither is present.
/// </remarks>
internal static class Aglc4Cases
{
    /// <summary>Writes the citation per AGLC4 case rules directly to <paramref name="writer"/>.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Write(CitationEntry entry, IBufferWriter<byte> writer)
    {
        Aglc4Writer.WriteBytes("*"u8, writer);
        Aglc4Writer.WriteString(entry.Title, writer);
        Aglc4Writer.WriteBytes("*"u8, writer);

        if (entry.LawReportSeries.Length > 0)
        {
            Aglc4Writer.WriteBytes(" "u8, writer);
            Aglc4Writer.WriteString(entry.LawReportSeries, writer);
            return;
        }

        if (entry.MediumNeutralCitation.Length > 0)
        {
            Aglc4Writer.WriteBytes(" "u8, writer);
            Aglc4Writer.WriteString(entry.MediumNeutralCitation, writer);
            return;
        }

        Aglc4Writer.WriteParenthesizedYear(entry.Year, writer);
    }
}
