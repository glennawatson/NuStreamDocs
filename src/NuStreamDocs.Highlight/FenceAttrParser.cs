// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Parses the per-block fence-info string the markdown emitter passes
/// through as <c>data-info</c>. Recognises the mkdocs-material /
/// pymdownx-superfences shape — space-separated <c>key="value"</c>
/// pairs (quoted) or bare <c>key=value</c> tokens.
/// </summary>
internal static class FenceAttrParser
{
    /// <summary>Bytes that may start a recognised attribute (case-sensitive lower-ASCII first letters).</summary>
    private static readonly SearchValues<byte> AttrStarts = SearchValues.Create("htl"u8);

    /// <summary>Tries to find a <c>title</c> attribute value in <paramref name="info"/>.</summary>
    /// <param name="info">UTF-8 fence-info bytes (already HTML-decoded by the caller).</param>
    /// <param name="value">Resolved title value bytes on success.</param>
    /// <returns>True when a title attribute was located.</returns>
    public static bool TryGetTitle(ReadOnlySpan<byte> info, out ReadOnlySpan<byte> value) =>
        TryGet(info, "title"u8, out value);

    /// <summary>Tries to find a <c>linenums</c> attribute value (the start line number, e.g. <c>"1"</c>).</summary>
    /// <param name="info">UTF-8 fence-info bytes.</param>
    /// <param name="value">Resolved value bytes on success.</param>
    /// <returns>True when found.</returns>
    public static bool TryGetLineNums(ReadOnlySpan<byte> info, out ReadOnlySpan<byte> value) =>
        TryGet(info, "linenums"u8, out value);

    /// <summary>Tries to find a <c>hl_lines</c> attribute value (e.g. <c>"2 4-6"</c>).</summary>
    /// <param name="info">UTF-8 fence-info bytes.</param>
    /// <param name="value">Resolved value bytes on success.</param>
    /// <returns>True when found.</returns>
    public static bool TryGetHighlightLines(ReadOnlySpan<byte> info, out ReadOnlySpan<byte> value) =>
        TryGet(info, "hl_lines"u8, out value);

    /// <summary>Generic <c>key=...</c> lookup against <paramref name="info"/>.</summary>
    /// <param name="info">UTF-8 fence-info bytes.</param>
    /// <param name="key">Attribute name (lowercase ASCII).</param>
    /// <param name="value">Resolved value bytes on success.</param>
    /// <returns>True when the attribute was found.</returns>
    private static bool TryGet(ReadOnlySpan<byte> info, ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
    {
        value = default;
        var p = 0;
        while (p < info.Length)
        {
            // Fast first-letter filter — most fence-info bytes aren't a key prefix.
            var rel = info[p..].IndexOfAny(AttrStarts);
            if (rel < 0)
            {
                return false;
            }

            var start = p + rel;
            if (TryReadAttribute(info, start, key, out value, out var consumed))
            {
                return true;
            }

            p = consumed > start ? consumed : start + 1;
        }

        return false;
    }

    /// <summary>Tries to read a single <c>key=value</c> attribute starting at <paramref name="start"/>.</summary>
    /// <param name="info">Source bytes.</param>
    /// <param name="start">Candidate offset.</param>
    /// <param name="key">Expected key bytes.</param>
    /// <param name="value">Captured value on success.</param>
    /// <param name="consumed">Offset to resume scanning from.</param>
    /// <returns>True on a successful key match.</returns>
    private static bool TryReadAttribute(ReadOnlySpan<byte> info, int start, ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value, out int consumed)
    {
        value = default;
        consumed = start;
        if (start > 0 && info[start - 1] is not (byte)' ' and not (byte)'\t')
        {
            return false;
        }

        if (start + key.Length >= info.Length || !info[start..].StartsWith(key) || info[start + key.Length] is not (byte)'=')
        {
            return false;
        }

        var afterEq = start + key.Length + 1;
        if (afterEq >= info.Length)
        {
            return false;
        }

        var first = info[afterEq];
        if (first is (byte)'"' or (byte)'\'')
        {
            return TryReadQuoted(info, afterEq, first, out value, out consumed);
        }

        return TryReadBare(info, afterEq, out value, out consumed);
    }

    /// <summary>Reads a quoted value (matching closing quote required).</summary>
    /// <param name="info">Source.</param>
    /// <param name="quoteOffset">Offset of opening quote.</param>
    /// <param name="quote">Quote byte.</param>
    /// <param name="value">Value span.</param>
    /// <param name="consumed">Offset just past the closing quote.</param>
    /// <returns>True when the quote was matched.</returns>
    private static bool TryReadQuoted(ReadOnlySpan<byte> info, int quoteOffset, byte quote, out ReadOnlySpan<byte> value, out int consumed)
    {
        value = default;
        consumed = quoteOffset + 1;
        var valStart = quoteOffset + 1;
        var endRel = info[valStart..].IndexOf(quote);
        if (endRel < 0)
        {
            return false;
        }

        value = info.Slice(valStart, endRel);
        consumed = valStart + endRel + 1;
        return true;
    }

    /// <summary>Reads a bare (unquoted) value up to the next whitespace.</summary>
    /// <param name="info">Source.</param>
    /// <param name="start">Value start offset.</param>
    /// <param name="value">Value span.</param>
    /// <param name="consumed">Offset of the terminating whitespace (or end of buffer).</param>
    /// <returns>True (always — bare values can be empty).</returns>
    private static bool TryReadBare(ReadOnlySpan<byte> info, int start, out ReadOnlySpan<byte> value, out int consumed)
    {
        var p = start;
        while (p < info.Length && info[p] is not (byte)' ' and not (byte)'\t')
        {
            p++;
        }

        value = info[start..p];
        consumed = p;
        return true;
    }
}
