// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Byte-level UTF-8 scanner that walks markdown source and finds
/// pandoc-style citation markers — <c>[@key]</c>, <c>[@key, p 23]</c>,
/// <c>[@a; @b]</c> — outside fenced and inline code regions.
/// </summary>
/// <remarks>
/// Match grammar (a deliberate subset of pandoc's full citation syntax;
/// covers the AGLC4 use case without the <c>[see @key]</c> /
/// <c>[suppress-author]</c> prefix variants):
/// <list type="bullet">
/// <item><c>[@key]</c></item>
/// <item><c>[@key, p 23]</c> — locator after a comma</item>
/// <item><c>[@key, p 23-25]</c></item>
/// <item><c>[@a; @b]</c> — semicolon-separated list of refs</item>
/// </list>
/// </remarks>
internal static class CitationMarkerScanner
{
    /// <summary>Length of the <c>[@</c> opening sequence — keeps the bounds-check magic-number-free.</summary>
    private const int MarkerOpenLength = 2;

    /// <summary>Bytes that may contain a marker open (<c>[</c>) — single-byte SearchValue keeps the scan vectorised.</summary>
    private static readonly SearchValues<byte> OpenChar = SearchValues.Create("["u8);

    /// <summary>Walks <paramref name="source"/> and yields one marker per recognised <c>[@key…]</c> span.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <returns>Markers in source order; empty when none.</returns>
    public static IReadOnlyList<CitationMarker> Find(ReadOnlySpan<byte> source)
    {
        var markers = new List<CitationMarker>();
        var cursor = 0;
        while (cursor < source.Length)
        {
            cursor = AdvanceOne(source, cursor, markers);
        }

        return markers;
    }

    /// <summary>Processes one byte (or one code region) at <paramref name="cursor"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Current scan offset.</param>
    /// <param name="markers">Accumulator.</param>
    /// <returns>Next scan offset.</returns>
    private static int AdvanceOne(ReadOnlySpan<byte> source, int cursor, List<CitationMarker> markers)
    {
        if (TrySkipCodeRegion(source, cursor, out var afterCode))
        {
            return afterCode;
        }

        var rel = source[cursor..].IndexOfAny(OpenChar);
        if (rel < 0)
        {
            return source.Length;
        }

        var bracketStart = cursor + rel;
        if (TryParseMarker(source, bracketStart, out var marker))
        {
            markers.Add(marker);
            return marker.EndIndex;
        }

        return bracketStart + 1;
    }

    /// <summary>If <paramref name="cursor"/> sits at a fenced or inline code region, returns the offset just past it.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Current offset.</param>
    /// <param name="afterCode">Offset just past the code region on success.</param>
    /// <returns>True when a code region was skipped.</returns>
    private static bool TrySkipCodeRegion(ReadOnlySpan<byte> source, int cursor, out int afterCode)
    {
        afterCode = cursor;
        if (MarkdownCodeScanner.AtLineStart(source, cursor)
            && MarkdownCodeScanner.TryConsumeFence(source, cursor, out var fenceEnd))
        {
            afterCode = fenceEnd;
            return true;
        }

        if (cursor >= source.Length || source[cursor] is not (byte)'`')
        {
            return false;
        }

        var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, cursor);
        if (inlineEnd <= cursor)
        {
            return false;
        }

        afterCode = inlineEnd;
        return true;
    }

    /// <summary>Tries to parse a marker starting at <paramref name="bracketStart"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="bracketStart">Offset of <c>[</c>.</param>
    /// <param name="marker">Parsed marker on success.</param>
    /// <returns>True when a well-formed marker was found.</returns>
    private static bool TryParseMarker(ReadOnlySpan<byte> source, int bracketStart, out CitationMarker marker)
    {
        marker = default;
        if (bracketStart + MarkerOpenLength >= source.Length || source[bracketStart + 1] is not (byte)'@')
        {
            return false;
        }

        var closeRel = source[bracketStart..].IndexOf((byte)']');
        if (closeRel < 0)
        {
            return false;
        }

        var bracketEnd = bracketStart + closeRel;
        var inner = source.Slice(bracketStart + 1, closeRel - 1);
        var cites = ParseInner(inner);
        if (cites.Length is 0)
        {
            return false;
        }

        marker = new(bracketStart, bracketEnd + 1, cites);
        return true;
    }

    /// <summary>Splits the inner content on <c>;</c> and parses each <c>@key…</c> reference.</summary>
    /// <param name="inner">Bytes between <c>[</c> and <c>]</c> (excluding the brackets themselves).</param>
    /// <returns>Parsed citation references; empty when malformed.</returns>
    private static CitationReference[] ParseInner(ReadOnlySpan<byte> inner)
    {
        var refs = new List<CitationReference>();
        var p = 0;
        while (p < inner.Length)
        {
            p = SkipSpaces(inner, p);
            if (p >= inner.Length || inner[p] is not (byte)'@')
            {
                return [];
            }

            p++;
            if (!TryReadReference(inner, p, out var reference, out var consumed))
            {
                return [];
            }

            refs.Add(reference);
            p = consumed;
            p = SkipSpaces(inner, p);
            if (p < inner.Length && inner[p] is (byte)';')
            {
                p++;
                continue;
            }

            if (p < inner.Length)
            {
                return [];
            }
        }

        return [.. refs];
    }

    /// <summary>Reads one <c>key</c> + optional <c>, locator</c> starting at <paramref name="offset"/>.</summary>
    /// <param name="inner">Inner bytes.</param>
    /// <param name="offset">Offset just past the <c>@</c>.</param>
    /// <param name="reference">Parsed reference on success.</param>
    /// <param name="consumed">Offset to resume reading from.</param>
    /// <returns>True on a successful parse.</returns>
    private static bool TryReadReference(ReadOnlySpan<byte> inner, int offset, out CitationReference reference, out int consumed)
    {
        reference = default;
        consumed = offset;
        var keyEnd = offset;
        while (keyEnd < inner.Length && IsKeyByte(inner[keyEnd]))
        {
            keyEnd++;
        }

        if (keyEnd == offset)
        {
            return false;
        }

        var key = Encoding.UTF8.GetString(inner[offset..keyEnd]);
        consumed = keyEnd;
        var locator = CitationLocator.None;
        var afterKey = SkipSpaces(inner, keyEnd);
        if (afterKey < inner.Length && inner[afterKey] is (byte)',')
        {
            var afterComma = SkipSpaces(inner, afterKey + 1);
            var locatorEnd = afterComma;
            while (locatorEnd < inner.Length && inner[locatorEnd] is not (byte)';')
            {
                locatorEnd++;
            }

            locator = ParseLocator(inner[afterComma..locatorEnd]);
            consumed = locatorEnd;
        }

        reference = new(key, locator);
        return true;
    }

    /// <summary>Splits a locator span into kind + value bytes (<c>"p 23"</c> → <c>(Page, "23")</c>; bare numerics → <c>(None, "23")</c>).</summary>
    /// <param name="bytes">Raw locator bytes (may have leading/trailing whitespace).</param>
    /// <returns>The parsed locator.</returns>
    private static CitationLocator ParseLocator(ReadOnlySpan<byte> bytes)
    {
        var trimmed = TrimAscii(bytes);
        if (trimmed.IsEmpty)
        {
            return CitationLocator.None;
        }

        var spaceIdx = trimmed.IndexOf((byte)' ');
        if (spaceIdx < 0)
        {
            return new(LocatorKind.None, Encoding.UTF8.GetString(trimmed));
        }

        var labelBytes = trimmed[..spaceIdx];
        var valueBytes = TrimAsciiStart(trimmed[(spaceIdx + 1)..]);
        var kind = LocatorLabel.Classify(labelBytes);
        var value = kind is LocatorKind.Other
            ? $"{Encoding.UTF8.GetString(labelBytes)} {Encoding.UTF8.GetString(valueBytes)}"
            : Encoding.UTF8.GetString(valueBytes);
        return new(kind, value);
    }

    /// <summary>Trims ASCII spaces / tabs from both ends of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">Input.</param>
    /// <returns>Trimmed slice.</returns>
    private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> bytes)
    {
        var start = 0;
        while (start < bytes.Length && bytes[start] is (byte)' ' or (byte)'\t')
        {
            start++;
        }

        var end = bytes.Length;
        while (end > start && bytes[end - 1] is (byte)' ' or (byte)'\t')
        {
            end--;
        }

        return bytes[start..end];
    }

    /// <summary>Trims ASCII spaces / tabs from the start of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">Input.</param>
    /// <returns>Trimmed slice.</returns>
    private static ReadOnlySpan<byte> TrimAsciiStart(ReadOnlySpan<byte> bytes)
    {
        var start = 0;
        while (start < bytes.Length && bytes[start] is (byte)' ' or (byte)'\t')
        {
            start++;
        }

        return bytes[start..];
    }

    /// <summary>Skips ASCII spaces forward.</summary>
    /// <param name="source">Source span.</param>
    /// <param name="offset">Start offset.</param>
    /// <returns>Offset of the first non-space byte.</returns>
    private static int SkipSpaces(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && source[p] is (byte)' ' or (byte)'\t')
        {
            p++;
        }

        return p;
    }

    /// <summary>True for ASCII bytes valid in a citation key (matches pandoc's own grammar).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True when allowed.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Single constant-pattern OR — JIT compiles to direct comparisons.")]
    private static bool IsKeyByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'_'
          or (byte)'-'
          or (byte)':'
          or (byte)'.'
          or (byte)'/';
}
