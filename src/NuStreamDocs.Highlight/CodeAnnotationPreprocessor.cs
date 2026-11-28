// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Scans unescaped code-block bytes for inline diff/focus directives (<c>// [!code ++]</c>,
/// <c>// [!code --]</c>, <c>// [!code focus]</c>, and <c>#</c> equivalents) and MkDocs Material
/// numbered callout comments (<c>// (1)!</c>, <c># (1)!</c>), stripping the markers from the
/// displayed code and recording per-line annotations.
/// </summary>
internal static class CodeAnnotationPreprocessor
{
    /// <summary>Maximum supported callout number in <c>(N)!</c> markers.</summary>
    private const int MaxCalloutNumber = 99;

    /// <summary>Minimum byte length of <c>(1)!</c>.</summary>
    private const int MinCalloutPayloadLength = 4;

    /// <summary>Length of the <c>//</c> comment prefix.</summary>
    private const int SlashCommentLength = 2;

    /// <summary>Returns <c>true</c> when <paramref name="source"/> contains candidate inline annotation markers.</summary>
    /// <param name="source">Unescaped UTF-8 code bytes.</param>
    /// <returns><c>true</c> when <c>[!code </c> or <c>)!</c> is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MayContainAnnotations(ReadOnlySpan<byte> source) =>
        source.IndexOf("[!code "u8) >= 0 || source.IndexOf(")!"u8) >= 0;

    /// <summary>Extracts per-line annotations and builds a marker-stripped copy of <paramref name="body"/> (caller strips any trailing fence newline beforehand).</summary>
    /// <param name="body">Unescaped UTF-8 code bytes without a trailing fence newline.</param>
    /// <param name="cleanBody">Code bytes with trailing annotation comments stripped.</param>
    /// <param name="annotations">One entry per logical line in <paramref name="cleanBody"/>.</param>
    /// <param name="hasFocus"><c>true</c> when at least one line carries <see cref="LineAnnotationKind.Focused"/>.</param>
    /// <param name="hasCallouts"><c>true</c> when at least one line carries a positive callout number.</param>
    /// <returns><c>true</c> when at least one annotation was stripped; <c>false</c> when all markers were false positives.</returns>
    internal static bool TryExtract(
        ReadOnlySpan<byte> body,
        out byte[] cleanBody,
        out LineAnnotation[] annotations,
        out bool hasFocus,
        out bool hasCallouts)
    {
        var writer = new ArrayBufferWriter<byte>(body.Length);
        List<LineAnnotation> lineList = [];
        hasFocus = false;
        hasCallouts = false;
        var anyMatched = ScanLines(body, writer, lineList, ref hasFocus, ref hasCallouts);

        cleanBody = anyMatched ? writer.WrittenSpan.ToArray() : [];
        annotations = anyMatched ? [.. lineList] : [];
        return anyMatched;
    }

    /// <summary>Walks every <c>\n</c>-separated line in <paramref name="body"/>, stripping trailing annotations into <paramref name="writer"/>.</summary>
    /// <param name="body">Body without final trailing newline.</param>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="lineList">Destination annotation list.</param>
    /// <param name="hasFocus">Set to <c>true</c> when a focus line is seen.</param>
    /// <param name="hasCallouts">Set to <c>true</c> when a callout line is seen.</param>
    /// <returns><c>true</c> when at least one line had a recognized annotation.</returns>
    private static bool ScanLines(
        ReadOnlySpan<byte> body,
        ArrayBufferWriter<byte> writer,
        List<LineAnnotation> lineList,
        ref bool hasFocus,
        ref bool hasCallouts)
    {
        var anyMatched = false;
        var remaining = body;
        var first = true;
        while (true)
        {
            if (!first)
            {
                WriteByte(writer, (byte)'\n');
            }

            first = false;
            var line = ReadNextLine(ref remaining, out var hasMore);
            var annotation = AppendCleanedLine(writer, line);
            RecordFlags(annotation, ref anyMatched, ref hasFocus, ref hasCallouts);
            lineList.Add(annotation);

            if (!hasMore)
            {
                return anyMatched;
            }
        }
    }

    /// <summary>Updates aggregate flags from <paramref name="annotation"/>.</summary>
    /// <param name="annotation">Parsed line annotation.</param>
    /// <param name="anyMatched">Set when any annotation is active.</param>
    /// <param name="hasFocus">Set when focus directive is active.</param>
    /// <param name="hasCallouts">Set when callout number is positive.</param>
    private static void RecordFlags(
        in LineAnnotation annotation,
        ref bool anyMatched,
        ref bool hasFocus,
        ref bool hasCallouts)
    {
        if (annotation.Kind != LineAnnotationKind.None || annotation.CalloutNumber > 0)
        {
            anyMatched = true;
        }

        if (annotation.Kind == LineAnnotationKind.Focused)
        {
            hasFocus = true;
        }

        if (annotation.CalloutNumber > 0)
        {
            hasCallouts = true;
        }
    }

    /// <summary>Strips a recognized trailing directive or callout comment from <paramref name="line"/> and writes the clean text to <paramref name="writer"/>.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="line">Single line without <c>\n</c>.</param>
    /// <returns>Parsed annotation metadata.</returns>
    private static LineAnnotation AppendCleanedLine(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> line)
    {
        var hasCr = !line.IsEmpty && line[^1] == (byte)'\r';
        var core = hasCr ? line[..^1] : line;
        var trimmedEnd = core.TrimEnd(" \t"u8);

        if (!TryMatchCommentSuffix(trimmedEnd, out var beforeComment, out var kind, out var callout))
        {
            WriteBytes(writer, line);
            return default;
        }

        WriteBytes(writer, beforeComment.TrimEnd(" \t"u8));
        if (hasCr)
        {
            WriteByte(writer, (byte)'\r');
        }

        return new(kind, callout);
    }

    /// <summary>Checks whether <paramref name="trimmedEnd"/> ends with a <c>//</c> or <c>#</c> annotation comment.</summary>
    /// <param name="trimmedEnd">Right-trimmed line bytes.</param>
    /// <param name="beforeComment">Slice preceding the comment marker.</param>
    /// <param name="kind">Matched directive kind.</param>
    /// <param name="callout">Matched callout number.</param>
    /// <returns><c>true</c> when a valid annotation comment was matched.</returns>
    private static bool TryMatchCommentSuffix(
        ReadOnlySpan<byte> trimmedEnd,
        out ReadOnlySpan<byte> beforeComment,
        out LineAnnotationKind kind,
        out int callout)
    {
        var slashIdx = trimmedEnd.LastIndexOf("//"u8);
        if (slashIdx >= 0
            && IsCommentBoundary(trimmedEnd, slashIdx)
            && TryParsePayload(trimmedEnd[(slashIdx + SlashCommentLength)..].TrimStart(" \t"u8), out kind, out callout))
        {
            beforeComment = trimmedEnd[..slashIdx];
            return true;
        }

        var hashIdx = trimmedEnd.LastIndexOf((byte)'#');
        if (hashIdx >= 0
            && IsCommentBoundary(trimmedEnd, hashIdx)
            && TryParsePayload(trimmedEnd[(hashIdx + 1)..].TrimStart(" \t"u8), out kind, out callout))
        {
            beforeComment = trimmedEnd[..hashIdx];
            return true;
        }

        beforeComment = default;
        kind = LineAnnotationKind.None;
        callout = 0;
        return false;
    }

    /// <summary>Verifies a comment opener sits at column 0 or after whitespace.</summary>
    /// <param name="line">Line bytes.</param>
    /// <param name="commentStart">Offset of <c>//</c> or <c>#</c>.</param>
    /// <returns><c>true</c> when at start of line or preceded by space/tab.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCommentBoundary(ReadOnlySpan<byte> line, int commentStart) =>
        commentStart == 0 || line[commentStart - 1] is (byte)' ' or (byte)'\t';

    /// <summary>Parses the trimmed payload after <c>//</c> or <c>#</c> as either <c>[!code ...]</c> or <c>(N)!</c>.</summary>
    /// <param name="payload">Trimmed comment body.</param>
    /// <param name="kind">Directive kind.</param>
    /// <param name="callout">Callout number.</param>
    /// <returns><c>true</c> when recognized.</returns>
    private static bool TryParsePayload(
        ReadOnlySpan<byte> payload,
        out LineAnnotationKind kind,
        out int callout)
    {
        callout = 0;
        if (payload.SequenceEqual("[!code ++]"u8))
        {
            kind = LineAnnotationKind.Added;
            return true;
        }

        if (payload.SequenceEqual("[!code --]"u8))
        {
            kind = LineAnnotationKind.Deleted;
            return true;
        }

        if (payload.SequenceEqual("[!code focus]"u8))
        {
            kind = LineAnnotationKind.Focused;
            return true;
        }

        kind = LineAnnotationKind.None;
        if (payload.Length < MinCalloutPayloadLength || payload[0] != (byte)'(' || !payload.EndsWith(")!"u8))
        {
            return false;
        }

        var digits = payload[1..^SlashCommentLength];
        if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number is >= 1 and <= MaxCalloutNumber)
        {
            callout = number;
            return true;
        }

        return false;
    }

    /// <summary>Slices the next <c>\n</c>-delimited line from <paramref name="remaining"/>.</summary>
    /// <param name="remaining">Remaining buffer.</param>
    /// <param name="hasMore"><c>true</c> when a <c>\n</c> was consumed and another line follows.</param>
    /// <returns>Current line slice.</returns>
    private static ReadOnlySpan<byte> ReadNextLine(ref ReadOnlySpan<byte> remaining, out bool hasMore)
    {
        var nl = remaining.IndexOf((byte)'\n');
        if (nl < 0)
        {
            var last = remaining;
            remaining = default;
            hasMore = false;
            return last;
        }

        var slice = remaining[..nl];
        remaining = remaining[(nl + 1)..];
        hasMore = true;
        return slice;
    }

    /// <summary>Writes a single byte into <paramref name="writer"/>.</summary>
    /// <param name="writer">Destination buffer writer.</param>
    /// <param name="value">Byte value.</param>
    private static void WriteByte(ArrayBufferWriter<byte> writer, byte value)
    {
        writer.GetSpan(1)[0] = value;
        writer.Advance(1);
    }

    /// <summary>Writes <paramref name="bytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">Destination buffer writer.</param>
    /// <param name="bytes">Bytes to copy.</param>
    private static void WriteBytes(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var span = writer.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        writer.Advance(bytes.Length);
    }
}
