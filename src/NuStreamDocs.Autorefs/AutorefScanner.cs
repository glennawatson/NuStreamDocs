// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Stateless byte-level helpers shared by the rewriter's logged and
/// unlogged passes. Pulled out so the marker-locate / id-extract /
/// terminator-detect logic can be unit-tested directly without
/// driving a whole rewrite pass.
/// </summary>
internal static class AutorefScanner
{
    /// <summary>Bytes that terminate an autoref ID inside an HTML attribute or text run.</summary>
    private static readonly SearchValues<byte> IdTerminators =
        SearchValues.Create("\"' <>)\n\r\t"u8);

    /// <summary>Gets the UTF-8 marker prefix that introduces an autoref reference.</summary>
    public static ReadOnlySpan<byte> Marker => "@autoref:"u8;

    /// <summary>Finds the next <c>@autoref:&lt;id&gt;</c> in <paramref name="source"/> starting at <paramref name="cursor"/>.</summary>
    /// <param name="source">UTF-8 input.</param>
    /// <param name="cursor">Search-start offset.</param>
    /// <param name="match">Captured offsets on success.</param>
    /// <returns>True when a marker was found.</returns>
    public static bool TryFindNext(ReadOnlySpan<byte> source, int cursor, out AutorefMatch match)
    {
        match = default;
        if (cursor >= source.Length)
        {
            return false;
        }

        var idx = source[cursor..].IndexOf(Marker);
        if (idx < 0)
        {
            return false;
        }

        var markerStart = cursor + idx;
        var idStart = markerStart + Marker.Length;
        var idEnd = FindIdEnd(source, idStart);
        match = new AutorefMatch(markerStart, idStart, idEnd);
        return true;
    }

    /// <summary>Decodes the ID covered by <paramref name="match"/> as a UTF-16 string.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="match">Match covering the marker + ID.</param>
    /// <returns>The decoded ID; empty when the match is empty.</returns>
    public static string DecodeId(ReadOnlySpan<byte> source, in AutorefMatch match)
    {
        var length = match.IdEnd - match.IdStart;
        return length <= 0 ? string.Empty : Encoding.UTF8.GetString(source.Slice(match.IdStart, length));
    }

    /// <summary>Returns the index just past the last ID byte starting at <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Index of the first ID byte.</param>
    /// <returns>Index just past the last ID byte (or <paramref name="source"/>'s length when no terminator is found).</returns>
    public static int FindIdEnd(ReadOnlySpan<byte> source, int start)
    {
        if (start >= source.Length)
        {
            return source.Length;
        }

        var hit = source[start..].IndexOfAny(IdTerminators);
        return hit < 0 ? source.Length : start + hit;
    }
}
