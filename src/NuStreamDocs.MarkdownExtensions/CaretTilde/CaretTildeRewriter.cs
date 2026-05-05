// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.CaretTilde;

/// <summary>
/// Stateless UTF-8 rewriter that emits <c>&lt;sup&gt;</c>,
/// <c>&lt;ins&gt;</c>, <c>&lt;sub&gt;</c>, and <c>&lt;del&gt;</c>
/// markup for the caret/tilde marker family. Runs as a
/// preprocessor so the downstream inline pass treats the
/// resulting tags as raw HTML (CommonMark §6.6).
/// </summary>
internal static class CaretTildeRewriter
{
    /// <summary>Width of a single-character marker (<c>^</c> or <c>~</c>).</summary>
    private const int SingleMarker = 1;

    /// <summary>Width of a double-character marker (<c>^^</c> or <c>~~</c>).</summary>
    private const int DoubleMarker = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        CodeAwareRewriter.Run(source, writer, TryRewriteMarker);

    /// <summary>Tries to match a caret/tilde marker pair starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a substitution was emitted.</returns>
    private static bool TryRewriteMarker(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        var marker = source[offset];
        if (marker is not (byte)'^' and not (byte)'~')
        {
            return false;
        }

        var doubled = offset + 1 < source.Length && source[offset + 1] == marker;
        var width = doubled ? DoubleMarker : SingleMarker;
        if (!TryFindClose(source, marker, offset + width, width, out var contentEnd))
        {
            return false;
        }

        WriteOpenTag(writer, marker, doubled);
        writer.Write(source[(offset + width)..contentEnd]);
        WriteCloseTag(writer, marker, doubled);
        consumed = contentEnd + width - offset;
        return true;
    }

    /// <summary>Writes the open tag bytes for the given marker shape.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="marker">Either <c>^</c> or <c>~</c>.</param>
    /// <param name="doubled">True for the doubled marker form.</param>
    private static void WriteOpenTag(IBufferWriter<byte> writer, byte marker, bool doubled)
    {
        var tag = (marker, doubled) switch
        {
            ((byte)'^', false) => "<sup>"u8,
            ((byte)'^', true) => "<ins>"u8,
            ((byte)'~', false) => "<sub>"u8,
            _ => "<del>"u8
        };
        writer.Write(tag);
    }

    /// <summary>Writes the close tag bytes for the given marker shape.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="marker">Either <c>^</c> or <c>~</c>.</param>
    /// <param name="doubled">True for the doubled marker form.</param>
    private static void WriteCloseTag(IBufferWriter<byte> writer, byte marker, bool doubled)
    {
        var tag = (marker, doubled) switch
        {
            ((byte)'^', false) => "</sup>"u8,
            ((byte)'^', true) => "</ins>"u8,
            ((byte)'~', false) => "</sub>"u8,
            _ => "</del>"u8
        };
        writer.Write(tag);
    }

    /// <summary>Finds the closing run of <paramref name="marker"/> bytes of the requested <paramref name="width"/> on the same line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="marker">Marker byte (<c>^</c> or <c>~</c>).</param>
    /// <param name="start">Offset just past the opening marker.</param>
    /// <param name="width">Marker width — 1 or 2.</param>
    /// <param name="contentEnd">Offset of the first closing-marker byte on success.</param>
    /// <returns>True when a valid close was found.</returns>
    private static bool TryFindClose(ReadOnlySpan<byte> source, byte marker, int start, int width, out int contentEnd)
    {
        contentEnd = 0;
        if (start >= source.Length || source[start] is (byte)' ' or (byte)'\n' || source[start] == marker)
        {
            return false;
        }

        for (var p = start; p < source.Length; p++)
        {
            if (source[p] is (byte)'\n')
            {
                return false;
            }

            if (source[p] != marker || !IsClosingRun(source, p, width))
            {
                continue;
            }

            contentEnd = p;
            return true;
        }

        return false;
    }

    /// <summary>Returns true when <paramref name="source"/> at <paramref name="offset"/> begins a closing run of exactly <paramref name="width"/> marker bytes.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Candidate close-run offset.</param>
    /// <param name="width">Required run width.</param>
    /// <returns>True when the run width matches.</returns>
    private static bool IsClosingRun(ReadOnlySpan<byte> source, int offset, int width)
    {
        if (width == DoubleMarker)
        {
            return offset + 1 < source.Length && source[offset + 1] == source[offset];
        }

        // Single-marker close: must NOT be followed by another marker (else it's the doubled form).
        return offset + 1 >= source.Length || source[offset + 1] != source[offset];
    }
}
