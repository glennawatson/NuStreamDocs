// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

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
    /// <remarks>
    /// Excludes <c>(</c> and <c>)</c> so a trailing method signature like <c>(System.Int32,System.String)</c>
    /// is treated as part of the ID rather than truncating it.
    /// </remarks>
    private static readonly SearchValues<byte> IdTerminators =
        SearchValues.Create("\"' <>\n\r\t"u8);

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
        match = new(markerStart, idStart, idEnd);
        return true;
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
