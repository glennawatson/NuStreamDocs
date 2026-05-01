// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Html;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Autolink handler. Recognises <c>&lt;https://...&gt;</c> /
/// <c>&lt;mailto:...&gt;</c> / any scheme followed by <c>://</c>.
/// </summary>
internal static class AutoLink
{
    /// <summary>Less-than byte.</summary>
    private const byte Lt = (byte)'<';

    /// <summary>Greater-than byte.</summary>
    private const byte Gt = (byte)'>';

    /// <summary>Colon byte.</summary>
    private const byte Colon = (byte)':';

    /// <summary>Minimum scheme name length per CommonMark §6.5.</summary>
    private const int MinSchemeLength = 2;

    /// <summary>
    /// Handles an open angle bracket at <paramref name="pos"/>.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the close bracket on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when an autolink was emitted.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        var contentStart = pos + 1;
        var closeIndex = FindClose(source, contentStart);
        if (closeIndex < 0)
        {
            return false;
        }

        var content = source[contentStart..closeIndex];
        if (!IsAutolink(content))
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, pos, writer);

        Write("<a href=\""u8, writer);
        HtmlEscape.EscapeText(content, writer);
        Write("\">"u8, writer);
        HtmlEscape.EscapeText(content, writer);
        Write("</a>"u8, writer);

        pos = closeIndex + 1;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Locates the closing <c>&gt;</c> on the same line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="from">First byte to consider.</param>
    /// <returns>Index of the close, or -1.</returns>
    public static int FindClose(ReadOnlySpan<byte> source, int from)
    {
        for (var i = from; i < source.Length; i++)
        {
            switch (source[i])
            {
                case Gt:
                    return i;
                case (byte)'\n' or (byte)'\r' or (byte)' ' or Lt:
                    return -1;
            }
        }

        return -1;
    }

    /// <summary>True when <paramref name="content"/> is a CommonMark URI autolink.</summary>
    /// <param name="content">Slice between the angle brackets.</param>
    /// <returns>True when the slice has the form <c>scheme:rest</c>.</returns>
    public static bool IsAutolink(ReadOnlySpan<byte> content)
    {
        var colon = content.IndexOf(Colon);
        if (colon < MinSchemeLength)
        {
            return false;
        }

        if (!IsSchemeStart(content[0]))
        {
            return false;
        }

        for (var i = 1; i < colon; i++)
        {
            if (!IsSchemeChar(content[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when <paramref name="b"/> may start a URI scheme.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for ASCII letters.</returns>
    private static bool IsSchemeStart(byte b) =>
        b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z';

    /// <summary>True when <paramref name="b"/> may continue a URI scheme.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for letters, digits, <c>+</c>, <c>-</c>, <c>.</c>.</returns>
    private static bool IsSchemeChar(byte b) =>
        IsSchemeStart(b) || b is >= (byte)'0' and <= (byte)'9' or (byte)'+' or (byte)'-' or (byte)'.';

    /// <summary>Bulk-writes <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void Write(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
