// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search;

/// <summary>
/// Strips HTML tags from a UTF-8 byte span and writes the visible
/// text to an <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <remarks>
/// Works on rendered HTML (the output of the markdown emitter), not
/// on arbitrary user-supplied HTML; we control the tags so we can
/// stay simple — track whether we're inside a tag or a script/style
/// block, collapse whitespace runs, drop everything else.
/// <para>
/// The first <c>&lt;h1&gt;</c> seen is captured separately as the
/// page title so the search index can surface it on result hits.
/// </para>
/// </remarks>
public static class HtmlTextExtractor
{
    /// <summary>Less-than byte (tag open).</summary>
    private const byte Lt = (byte)'<';

    /// <summary>Greater-than byte (tag close).</summary>
    private const byte Gt = (byte)'>';

    /// <summary>Slash byte (tag end-marker).</summary>
    private const byte Slash = (byte)'/';

    /// <summary>Offset between ASCII upper and lower case bytes.</summary>
    private const byte AsciiCaseShift = 32;

    /// <summary>Space byte.</summary>
    private const byte Sp = (byte)' ';

    /// <summary>
    /// Walks <paramref name="html"/> and extracts visible text + first H1 title.
    /// </summary>
    /// <param name="html">UTF-8 HTML bytes.</param>
    /// <param name="text">UTF-8 sink for the stripped body text.</param>
    /// <returns>The first H1 contents as a UTF-8 byte array, or empty when none.</returns>
    public static byte[] Extract(ReadOnlySpan<byte> html, IBufferWriter<byte> text)
    {
        ArgumentNullException.ThrowIfNull(text);

        using var titleRental = PageBuilderPool.Rent();
        var titleBuffer = titleRental.Writer;
        var state = new ExtractState(InsideTag: false, InsideScript: false, CapturingTitle: false, EmittedSpace: true, TitleAlreadyCaptured: false);

        for (var i = 0; i < html.Length; i++)
        {
            var b = html[i];
            if (state.InsideTag)
            {
                state = AdvancePastTag(html, i, state, titleBuffer);
                if (b == Gt)
                {
                    state = state with { InsideTag = false };
                }

                continue;
            }

            if (b == Lt)
            {
                state = OpenTag(html, i, state);
                continue;
            }

            if (state.InsideScript)
            {
                continue;
            }

            EmitTextByte(b, state, text, titleBuffer, out state);
        }

        return [.. titleBuffer.WrittenSpan];
    }

    /// <summary>Handles a single non-tag byte.</summary>
    /// <param name="b">Source byte.</param>
    /// <param name="state">Walker state.</param>
    /// <param name="text">Text sink.</param>
    /// <param name="title">Title sink.</param>
    /// <param name="next">Updated state.</param>
    private static void EmitTextByte(byte b, in ExtractState state, IBufferWriter<byte> text, IBufferWriter<byte> title, out ExtractState next)
    {
        if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            if (state.EmittedSpace)
            {
                next = state;
                return;
            }

            WriteByte(text, Sp);
            if (state.CapturingTitle)
            {
                WriteByte(title, Sp);
            }

            next = state with { EmittedSpace = true };
            return;
        }

        WriteByte(text, b);
        if (state.CapturingTitle)
        {
            WriteByte(title, b);
        }

        next = state with { EmittedSpace = false };
    }

    /// <summary>Reacts to a <c>&lt;</c> by inspecting the upcoming tag name.</summary>
    /// <param name="html">Source bytes.</param>
    /// <param name="ltIndex">Index of the <c>&lt;</c>.</param>
    /// <param name="state">Walker state.</param>
    /// <returns>Updated state with InsideTag set, plus title-capture / script flags.</returns>
    private static ExtractState OpenTag(ReadOnlySpan<byte> html, int ltIndex, in ExtractState state)
    {
        var name = ReadTagName(html, ltIndex + 1);
        var isClose = ltIndex + 1 < html.Length && html[ltIndex + 1] == Slash;

        if (TagEquals(name, "script"u8) || TagEquals(name, "style"u8))
        {
            return state with { InsideTag = true, InsideScript = !isClose };
        }

        if (!TagEquals(name, "h1"u8))
        {
            return state with { InsideTag = true };
        }

        // Only capture the FIRST h1 — TitleAlreadyCaptured latches
        // once we've finished capturing so subsequent h1 elements
        // never re-enter capture mode.
        if (isClose && state.CapturingTitle)
        {
            return state with { InsideTag = true, CapturingTitle = false, TitleAlreadyCaptured = true };
        }

        var startCapture = !isClose && !state.CapturingTitle && !state.TitleAlreadyCaptured;
        return state with
        {
            InsideTag = true,
            CapturingTitle = state.CapturingTitle || startCapture,
        };
    }

    /// <summary>No-op while inside a tag; preserved for future attribute-aware extraction.</summary>
    /// <param name="html">Source bytes.</param>
    /// <param name="i">Current index.</param>
    /// <param name="state">Walker state.</param>
    /// <param name="title">Title sink (unused here).</param>
    /// <returns>Unchanged state.</returns>
    private static ExtractState AdvancePastTag(ReadOnlySpan<byte> html, int i, in ExtractState state, ArrayBufferWriter<byte> title)
    {
        _ = html;
        _ = i;
        _ = title;
        return state;
    }

    /// <summary>Reads the tag-name run starting at <paramref name="from"/>, skipping a leading slash.</summary>
    /// <param name="html">Source bytes.</param>
    /// <param name="from">Start index (just past the <c>&lt;</c>).</param>
    /// <returns>Span pointing at the tag name (lowercased on the source if applicable).</returns>
    private static ReadOnlySpan<byte> ReadTagName(ReadOnlySpan<byte> html, int from)
    {
        if (from < html.Length && html[from] == Slash)
        {
            from++;
        }

        var end = from;
        while (end < html.Length && IsTagNameByte(html[end]))
        {
            end++;
        }

        return html[from..end];
    }

    /// <summary>True for ASCII alphanumerics; tag-name bytes per HTML's lax parsing.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for letters and digits.</returns>
    private static bool IsTagNameByte(byte b) =>
        b is >= (byte)'a' and <= (byte)'z'
            or >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'0' and <= (byte)'9';

    /// <summary>Case-insensitive ASCII tag-name comparison.</summary>
    /// <param name="actual">Candidate name bytes.</param>
    /// <param name="expected">Lowercase expected bytes.</param>
    /// <returns>True when <paramref name="actual"/> matches <paramref name="expected"/> ignoring ASCII case.</returns>
    private static bool TagEquals(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        for (var i = 0; i < actual.Length; i++)
        {
            var a = actual[i];
            if (a is >= (byte)'A' and <= (byte)'Z')
            {
                a = (byte)(a + AsciiCaseShift);
            }

            if (a != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Writes a single byte to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="b">Byte to emit.</param>
    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        var dst = writer.GetSpan(1);
        dst[0] = b;
        writer.Advance(1);
    }

    /// <summary>State carried through the byte walker.</summary>
    /// <param name="InsideTag">Currently between <c>&lt;</c> and <c>&gt;</c>.</param>
    /// <param name="InsideScript">Currently inside a <c>&lt;script&gt;</c> or <c>&lt;style&gt;</c> block.</param>
    /// <param name="CapturingTitle">Currently inside the first <c>&lt;h1&gt;</c>.</param>
    /// <param name="EmittedSpace">Last byte written to the text sink was whitespace; used to collapse runs.</param>
    /// <param name="TitleAlreadyCaptured">Sticky flag — once a <c>&lt;h1&gt;</c> close has fired we never re-enter capture mode.</param>
    private readonly record struct ExtractState(bool InsideTag, bool InsideScript, bool CapturingTitle, bool EmittedSpace, bool TitleAlreadyCaptured);
}
