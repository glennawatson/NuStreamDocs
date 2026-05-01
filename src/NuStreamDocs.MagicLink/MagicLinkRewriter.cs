// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using static NuStreamDocs.Markdown.Common.MarkdownCodeScanner;

namespace NuStreamDocs.MagicLink;

/// <summary>
/// Stateless UTF-8 magic-link rewriter. Wraps bare URLs in
/// CommonMark autolink brackets so the downstream inline renderer
/// emits anchor tags. Skips fenced-code, inline-code, and any
/// already-linked region (<c>[…](…)</c> or <c>&lt;url&gt;</c>).
/// </summary>
internal static class MagicLinkRewriter
{
    /// <summary>Sentence-terminator bytes peeled off a permissive URL match.</summary>
    private static readonly SearchValues<byte> TrailingPunctuation = SearchValues.Create(".,;:!?)]}'*_"u8);

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (AtLineStart(source, i) && TryConsumeFence(source, i, out var fenceEnd))
            {
                writer.Write(source[i..fenceEnd]);
                i = fenceEnd;
                continue;
            }

            switch (source[i])
            {
                case (byte)'`':
                    {
                        var inlineEnd = ConsumeInlineCode(source, i);
                        writer.Write(source[i..inlineEnd]);
                        i = inlineEnd;
                        continue;
                    }

                case (byte)'<':
                    {
                        // Already an autolink or HTML — skip to its closing '>' verbatim so
                        // we don't double-wrap or wander into a tag's attributes.
                        var skipEnd = ConsumeAngleSpan(source, i);
                        writer.Write(source[i..skipEnd]);
                        i = skipEnd;
                        continue;
                    }

                case (byte)'[':
                    {
                        // A markdown link's label may legally contain a URL; preserve the
                        // [...](...) span verbatim instead of rewriting inside it.
                        var skipEnd = ConsumeBracketSpan(source, i);
                        writer.Write(source[i..skipEnd]);
                        i = skipEnd;
                        continue;
                    }
            }

            if (TryRewriteUrl(source, i, writer, out var consumed))
            {
                i += consumed;
                continue;
            }

            var dest = writer.GetSpan(1);
            dest[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Tries to wrap a bare URL starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor position.</param>
    /// <param name="writer">Sink for the autolink-wrapped URL.</param>
    /// <param name="consumed">Number of input bytes the match covered.</param>
    /// <returns>True when a URL was rewritten.</returns>
    private static bool TryRewriteUrl(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (!IsWordBoundaryBefore(source, offset))
        {
            return false;
        }

        var schemeLength = MatchScheme(source, offset);
        if (schemeLength is 0)
        {
            return false;
        }

        var afterScheme = offset + schemeLength;
        if (afterScheme >= source.Length || !IsUrlBodyStart(source[afterScheme]))
        {
            return false;
        }

        var bodyEnd = ScanUrlBody(source, afterScheme);
        var trimmed = TrimTrailingPunctuation(source, afterScheme, bodyEnd);
        if (trimmed <= afterScheme)
        {
            return false;
        }

        writer.Write("<"u8);
        writer.Write(source[offset..trimmed]);
        writer.Write(">"u8);
        consumed = trimmed - offset;
        return true;
    }

    /// <summary>Returns the byte length of the URL scheme starting at <paramref name="offset"/>; zero when no known scheme matches.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate start.</param>
    /// <returns>Scheme length including its delimiter.</returns>
    private static int MatchScheme(ReadOnlySpan<byte> source, int offset)
    {
        var slice = source[offset..];
        if (slice.StartsWith("https://"u8))
        {
            return "https://"u8.Length;
        }

        if (slice.StartsWith("http://"u8))
        {
            return "http://"u8.Length;
        }

        if (slice.StartsWith("ftps://"u8))
        {
            return "ftps://"u8.Length;
        }

        if (slice.StartsWith("ftp://"u8))
        {
            return "ftp://"u8.Length;
        }

        if (slice.StartsWith("mailto:"u8))
        {
            return "mailto:"u8.Length;
        }

        if (slice.StartsWith("www."u8))
        {
            return 0; // leave for now — bare `www.` autolinking needs scheme synthesis we don't yet do
        }

        return 0;
    }

    /// <summary>Returns true when <paramref name="b"/> can start a URL body.</summary>
    /// <param name="b">Byte just after the scheme.</param>
    /// <returns>True for a host/userinfo opener.</returns>
    private static bool IsUrlBodyStart(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'-' or (byte)'_' or (byte)'+' or (byte)'%';

    /// <summary>Scans forward over URL body bytes, returning the exclusive end.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position just past the scheme.</param>
    /// <returns>End of the URL body span.</returns>
    private static int ScanUrlBody(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && IsUrlBodyByte(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Returns true when <paramref name="b"/> is a permissible URL body byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True when the byte is part of a URL.</returns>
    private static bool IsUrlBodyByte(byte b) =>
        b is >= (byte)'!' and <= (byte)'~'
        and not (byte)'<' and not (byte)'>'
        and not (byte)'"' and not (byte)'\\'
        and not (byte)'`' and not (byte)' ';

    /// <summary>Trims trailing punctuation likely to be sentence terminators rather than URL parts.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="bodyStart">First byte of the URL body.</param>
    /// <param name="bodyEnd">Exclusive end after permissive scan.</param>
    /// <returns>Adjusted exclusive end.</returns>
    private static int TrimTrailingPunctuation(ReadOnlySpan<byte> source, int bodyStart, int bodyEnd)
    {
        var end = bodyEnd;
        while (end > bodyStart && TrailingPunctuation.Contains(source[end - 1]))
        {
            end--;
        }

        return end;
    }

    /// <summary>Returns true when <paramref name="offset"/> is at a word boundary on its left.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position to test.</param>
    /// <returns>True when boundary holds.</returns>
    private static bool IsWordBoundaryBefore(ReadOnlySpan<byte> source, int offset) =>
        offset is 0 || !IsWordByte(source[offset - 1]);

    /// <summary>Returns true when <paramref name="b"/> is an ASCII identifier byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True when classed as a word byte.</returns>
    private static bool IsWordByte(byte b) =>
        b is >= (byte)'0' and <= (byte)'9'
          or >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or (byte)'_';

    /// <summary>Consumes through a <c>&lt;…&gt;</c> span (autolink or HTML); advances past the closing <c>&gt;</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the opening <c>&lt;</c>.</param>
    /// <returns>Exclusive end after the close angle, or the input position + 1 when no close is found.</returns>
    private static int ConsumeAngleSpan(ReadOnlySpan<byte> source, int offset)
    {
        var rel = source[offset..].IndexOf((byte)'>');
        return rel < 0 ? offset + 1 : offset + rel + 1;
    }

    /// <summary>Consumes through a markdown link's bracket span — <c>[label](dest)</c> when present.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the opening <c>[</c>.</param>
    /// <returns>Exclusive end past the closing <c>)</c>, or the input position + 1 when the bracket is bare.</returns>
    private static int ConsumeBracketSpan(ReadOnlySpan<byte> source, int offset)
    {
        var labelClose = source[offset..].IndexOf((byte)']');
        if (labelClose < 0)
        {
            return offset + 1;
        }

        var afterLabel = offset + labelClose + 1;
        if (afterLabel >= source.Length || source[afterLabel] is not (byte)'(')
        {
            return afterLabel;
        }

        var destClose = source[afterLabel..].IndexOf((byte)')');
        return destClose < 0 ? afterLabel : afterLabel + destClose + 1;
    }
}
