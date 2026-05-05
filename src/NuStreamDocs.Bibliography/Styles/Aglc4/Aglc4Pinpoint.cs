// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Styles.Aglc4;

/// <summary>AGLC4 pinpoint locator formatter — the <c>p 23</c> / <c>[12]</c> / <c>ch 4</c> trailing portion.</summary>
internal static class Aglc4Pinpoint
{
    /// <summary>Writes the locator per AGLC4 conventions directly to <paramref name="writer"/>; the value bytes are sliced from <paramref name="source"/> using the locator's offsets.</summary>
    /// <param name="locator">Source locator.</param>
    /// <param name="source">Original source span the locator's offsets point into.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Write(in CitationLocator locator, ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var value = source.Slice(locator.Start, locator.Length);
        if (locator.Kind is LocatorKind.Paragraph)
        {
            Aglc4Writer.WriteBytes("["u8, writer);
            Aglc4Writer.WriteBytes(value, writer);
            Aglc4Writer.WriteBytes("]"u8, writer);
            return;
        }

        Aglc4Writer.WriteBytes(PrefixFor(locator.Kind), writer);
        Aglc4Writer.WriteBytes(value, writer);
    }

    /// <summary>Bytes to emit before the value (includes a trailing space when non-empty).</summary>
    /// <param name="kind">Classified locator kind.</param>
    /// <returns>Prefix bytes; empty for kinds that emit the value bare.</returns>
    private static ReadOnlySpan<byte> PrefixFor(LocatorKind kind) => kind switch
    {
        LocatorKind.Line => "l "u8,
        LocatorKind.Chapter => "ch "u8,
        LocatorKind.Section => "s "u8,
        LocatorKind.Schedule => "sch "u8,
        LocatorKind.Article => "art "u8,
        _ => default
    };
}
