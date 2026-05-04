// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Html;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>Helpers for locating the <c>{: ... }</c> attr-list marker in UTF-8 HTML and emitting the rewritten opening tag once a parsed attribute set is in hand.</summary>
internal static class AttrListMarker
{
    /// <summary>Stack-buffer cap for class tokens parsed out of one attr-list body. Real-world tokens fit comfortably; overflow is silently truncated.</summary>
    private const int MaxClassTokens = 16;

    /// <summary>Stack-buffer cap for key/value pairs parsed out of one attr-list body.</summary>
    private const int MaxKvPairs = 16;

    /// <summary>Sentinel start offset signalling "no token captured".</summary>
    private const int NoOffset = -1;

    /// <summary>Gets the UTF-8 bytes for the canonical Python-Markdown <c>{:</c> opening marker.</summary>
    public static ReadOnlySpan<byte> OpenMarker => "{:"u8;

    /// <summary>Returns the earliest opener offset (<c>{:</c> or <c>{ </c>) within <paramref name="span"/>, or -1 when neither is present.</summary>
    /// <param name="span">UTF-8 search span.</param>
    /// <returns>Earliest opener offset or -1.</returns>
    /// <remarks>The caller still re-validates via <see cref="TryMatchMarker"/> so the lead-byte lookahead applies.</remarks>
    public static int IndexOfOpener(ReadOnlySpan<byte> span)
    {
        var colonForm = span.IndexOf(OpenMarker);
        var spaceForm = span.IndexOf("{ "u8);
        var hashForm = span.IndexOf("{#"u8);
        var dotForm = span.IndexOf("{."u8);
        return MinNonNegative(MinNonNegative(colonForm, spaceForm), MinNonNegative(hashForm, dotForm));
    }

    /// <summary>Tries to match an optional-whitespace + <c>{:&#160;… }</c> / <c>{ … }</c> token starting at or just after <paramref name="p"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset (typically just after the closing <c>&gt;</c> or <c>&lt;/tag&gt;</c>).</param>
    /// <param name="contentStart">First byte of the inner attr-list text on success.</param>
    /// <param name="contentEnd">Offset of the closing <c>}</c> on success.</param>
    /// <param name="markerEnd">Offset just past the closing <c>}</c> on success.</param>
    /// <returns>True when a well-formed marker was found.</returns>
    /// <remarks>
    /// Accepts two opener shapes:
    /// <list type="bullet">
    ///   <item><description>The canonical Python-Markdown form <c>{: …}</c>.</description></item>
    ///   <item><description>The mkdocs-material shorthand <c>{ … }</c> (open-brace plus inner whitespace, no colon)
    ///     where the inner content starts with one of <c>.</c>, <c>#</c>, an ASCII-letter
    ///     attribute-name byte, or another whitespace/closing brace. The shape rules out the
    ///     overwhelming majority of code-block uses of <c>{</c> while still catching the
    ///     leading-space form mkdocs-material conventions emit.</description></item>
    /// </list>
    /// </remarks>
    public static bool TryMatchMarker(ReadOnlySpan<byte> source, int p, out int contentStart, out int contentEnd, out int markerEnd)
    {
        contentStart = NoOffset;
        contentEnd = NoOffset;
        markerEnd = NoOffset;

        if (!TryMatchOpener(source, p, out var afterOpener))
        {
            return false;
        }

        var inside = AsciiByteHelpers.SkipWhitespace(source, afterOpener);
        var closeRel = source[inside..].IndexOf((byte)'}');
        if (closeRel < 0)
        {
            return false;
        }

        var close = inside + closeRel;
        contentStart = inside;
        contentEnd = TrimTrailingWhitespace(source, inside, close);
        markerEnd = close + 1;
        return contentEnd >= contentStart;
    }

    /// <summary>Emits the merged attribute fragment (with leading space when non-empty) directly into <paramref name="sink"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="existingAttrsStart">Offset of the existing attribute fragment (between the tag name and the closing <c>&gt;</c>).</param>
    /// <param name="existingAttrsEnd">Offset just past the existing attribute fragment.</param>
    /// <param name="attrListStart">Offset of the inner attr-list text (between <c>{:</c> and <c>}</c>).</param>
    /// <param name="attrListEnd">Offset just past the inner attr-list text.</param>
    /// <param name="sink">UTF-8 sink to write the merged fragment into.</param>
    /// <remarks>
    /// No string materialization, no per-match heap allocation on typical input sizes —
    /// the parsed attr-list ranges live in stackalloc buffers bundled into <see cref="AttrListBuffers"/>.
    /// </remarks>
    public static void EmitMerged(
        ReadOnlySpan<byte> source,
        int existingAttrsStart,
        int existingAttrsEnd,
        int attrListStart,
        int attrListEnd,
        IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        Span<ByteRange> classBuffer = stackalloc ByteRange[MaxClassTokens];
        Span<KvRange> kvBuffer = stackalloc KvRange[MaxKvPairs];
        Span<bool> kvEmitted = stackalloc bool[MaxKvPairs];
        var buffers = new AttrListBuffers(classBuffer, kvBuffer, kvEmitted);

        ParseAttrList(source, attrListStart, attrListEnd, ref buffers);

        var pos = existingAttrsStart;
        while (TryReadExistingAttribute(source, ref pos, existingAttrsEnd, out var nameRange, out var valueRange))
        {
            EmitOneExistingAttr(source, nameRange, valueRange, ref buffers, sink);
        }

        if (buffers.IdRange.Length > 0 && !buffers.IdEmitted)
        {
            EmitIdAttr(source, buffers.IdRange, sink);
        }

        if (buffers.ClassCount > 0 && !buffers.ClassEmitted)
        {
            EmitClassAttr(source, default, buffers.Classes, sink);
        }

        for (var i = 0; i < buffers.KvCount; i++)
        {
            if (!buffers.KvEmitted[i])
            {
                EmitKvAttr(source, buffers.KvBuffer[i], sink);
            }
        }
    }

    /// <summary>Parses <paramref name="source"/>'s attr-list body into <paramref name="buffers"/>'s stack-local ranges.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="attrListStart">Body start.</param>
    /// <param name="attrListEnd">Body end.</param>
    /// <param name="buffers">Mutable parse-state buffers.</param>
    private static void ParseAttrList(ReadOnlySpan<byte> source, int attrListStart, int attrListEnd, ref AttrListBuffers buffers)
    {
        var i = attrListStart;
        while (i < attrListEnd)
        {
            var b = source[i];
            if (AsciiByteHelpers.IsAsciiWhitespace(b))
            {
                i++;
                continue;
            }

            if (b is (byte)'#')
            {
                i = ReadToken(source, i + 1, attrListEnd, out var range);
                if (range.Length > 0)
                {
                    buffers.IdRange = range;
                }

                continue;
            }

            if (b is (byte)'.')
            {
                i = ReadToken(source, i + 1, attrListEnd, out var range);
                buffers.AppendClass(range);
                continue;
            }

            i = ReadKeyValue(source, i, attrListEnd, ref buffers);
        }
    }

    /// <summary>Reads a non-whitespace token starting at <paramref name="offset"/> and bounded by <paramref name="end"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="range">Token range on success; zero-length when the offset starts on whitespace.</param>
    /// <returns>Cursor just past the token.</returns>
    private static int ReadToken(ReadOnlySpan<byte> source, int offset, int end, out ByteRange range)
    {
        var start = offset;
        while (offset < end && !AsciiByteHelpers.IsAsciiWhitespace(source[offset]))
        {
            offset++;
        }

        range = new(start, offset - start);
        return offset;
    }

    /// <summary>Reads one <c>key=value</c> (or bare <c>key</c>) pair at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="buffers">Mutable parse-state buffers.</param>
    /// <returns>Cursor just past the pair.</returns>
    private static int ReadKeyValue(ReadOnlySpan<byte> source, int offset, int end, ref AttrListBuffers buffers)
    {
        var keyStart = offset;
        while (offset < end && source[offset] is not ((byte)'=' or (byte)' ' or (byte)'\t'))
        {
            offset++;
        }

        var keyRange = new ByteRange(keyStart, offset - keyStart);
        if (offset >= end || source[offset] is not (byte)'=')
        {
            buffers.AppendKv(keyRange, new(NoOffset, 0));
            return offset;
        }

        offset++; // skip '='
        offset = ReadAttrListValue(source, offset, end, out var valueRange);
        buffers.AppendKv(keyRange, valueRange);
        return offset;
    }

    /// <summary>Reads an attr-list value (single- or double-quoted, or bare).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset (just past the <c>=</c>).</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="valueRange">Value range; sentinel start when no value follows.</param>
    /// <returns>Cursor just past the value.</returns>
    private static int ReadAttrListValue(ReadOnlySpan<byte> source, int offset, int end, out ByteRange valueRange)
    {
        if (offset >= end)
        {
            valueRange = new(NoOffset, 0);
            return offset;
        }

        var quote = source[offset];
        return quote is (byte)'"' or (byte)'\''
            ? ReadQuotedValue(source, offset + 1, end, quote, out valueRange)
            : ReadBareValue(source, offset, end, out valueRange);
    }

    /// <summary>Reads a quoted value bounded by <paramref name="quote"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Offset just past the opening quote.</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="quote">Quote byte (single or double).</param>
    /// <param name="valueRange">Value range (excluding the quotes).</param>
    /// <returns>Cursor just past the closing quote (or at end if unterminated).</returns>
    private static int ReadQuotedValue(ReadOnlySpan<byte> source, int offset, int end, byte quote, out ByteRange valueRange)
    {
        var start = offset;
        while (offset < end && source[offset] != quote)
        {
            offset++;
        }

        valueRange = new(start, offset - start);
        return offset < end ? offset + 1 : offset;
    }

    /// <summary>Reads a bare (unquoted) value up to the next whitespace.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset.</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="valueRange">Value range.</param>
    /// <returns>Cursor at the trailing whitespace or end.</returns>
    private static int ReadBareValue(ReadOnlySpan<byte> source, int offset, int end, out ByteRange valueRange)
    {
        var start = offset;
        while (offset < end && !AsciiByteHelpers.IsAsciiWhitespace(source[offset]))
        {
            offset++;
        }

        valueRange = new(start, offset - start);
        return offset;
    }

    /// <summary>Reads one HTML attribute (<c>name</c>, <c>name=value</c>, or <c>name="value"</c>) at <paramref name="pos"/>; returns ranges into <paramref name="source"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past whitespace and the matched attribute on success.</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="nameRange">Attribute name range on success.</param>
    /// <param name="valueRange">Attribute value range on success; sentinel start when no <c>=</c> follows.</param>
    /// <returns>True when an attribute was read; false at end-of-input.</returns>
    private static bool TryReadExistingAttribute(
        ReadOnlySpan<byte> source,
        ref int pos,
        int end,
        out ByteRange nameRange,
        out ByteRange valueRange)
    {
        pos = AsciiByteHelpers.SkipWhitespace(source, pos);
        if (pos >= end || !IsAttrNameStart(source[pos]))
        {
            if (pos < end)
            {
                pos++;
            }

            nameRange = new(NoOffset, 0);
            valueRange = new(NoOffset, 0);
            return pos < end;
        }

        pos = ReadAttrName(source, pos, end, out nameRange);
        if (pos >= end || source[pos] is not (byte)'=')
        {
            valueRange = new(NoOffset, 0);
            return true;
        }

        pos++; // skip '='
        pos = ReadHtmlAttributeValue(source, pos, end, out valueRange);
        return true;
    }

    /// <summary>Reads an HTML attribute name starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset (must be on an attribute-name start byte).</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="nameRange">Name range.</param>
    /// <returns>Cursor just past the name.</returns>
    private static int ReadAttrName(ReadOnlySpan<byte> source, int offset, int end, out ByteRange nameRange)
    {
        var start = offset;
        offset++;
        while (offset < end && IsAttrNameContinue(source[offset]))
        {
            offset++;
        }

        nameRange = new(start, offset - start);
        return offset;
    }

    /// <summary>Reads an HTML attribute value (double-quoted or bare).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Start offset (just past the <c>=</c>).</param>
    /// <param name="end">Hard end (exclusive).</param>
    /// <param name="valueRange">Value range.</param>
    /// <returns>Cursor just past the value.</returns>
    private static int ReadHtmlAttributeValue(ReadOnlySpan<byte> source, int offset, int end, out ByteRange valueRange)
    {
        if (offset >= end)
        {
            valueRange = new(NoOffset, 0);
            return offset;
        }

        return source[offset] is (byte)'"'
            ? ReadQuotedValue(source, offset + 1, end, (byte)'"', out valueRange)
            : ReadBareValue(source, offset, end, out valueRange);
    }

    /// <summary>Emits one existing attribute, applying value overrides for id / class / kv when the name matches the new set.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="nameRange">Existing attribute name range.</param>
    /// <param name="valueRange">Existing attribute value range.</param>
    /// <param name="buffers">Mutable parse-state buffers; emitted-flags are flipped when an override fires.</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void EmitOneExistingAttr(
        ReadOnlySpan<byte> source,
        in ByteRange nameRange,
        in ByteRange valueRange,
        ref AttrListBuffers buffers,
        IBufferWriter<byte> sink)
    {
        if (nameRange.Length is 0)
        {
            return;
        }

        var name = source.Slice(nameRange.Start, nameRange.Length);
        if (name.SequenceEqual("id"u8) && buffers.IdRange.Length > 0)
        {
            EmitIdAttr(source, buffers.IdRange, sink);
            buffers.IdEmitted = true;
            return;
        }

        if (name.SequenceEqual("class"u8) && buffers.ClassCount > 0)
        {
            EmitClassAttr(source, valueRange, buffers.Classes, sink);
            buffers.ClassEmitted = true;
            return;
        }

        var kvs = buffers.Kvs;
        for (var i = 0; i < kvs.Length; i++)
        {
            var kvKey = source.Slice(kvs[i].Key.Start, kvs[i].Key.Length);
            if (name.SequenceEqual(kvKey))
            {
                EmitKvAttr(source, kvs[i], sink);
                buffers.KvEmitted[i] = true;
                return;
            }
        }

        EmitVerbatimAttr(source, nameRange, valueRange, sink);
    }

    /// <summary>Emits <c>" name=\"value\""</c> verbatim from the source span (no escaping; the source is already well-formed).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="nameRange">Name range.</param>
    /// <param name="valueRange">Value range; sentinel start emits a bare attribute name.</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void EmitVerbatimAttr(ReadOnlySpan<byte> source, in ByteRange nameRange, in ByteRange valueRange, IBufferWriter<byte> sink)
    {
        Utf8StringWriter.WriteByte(sink, (byte)' ');
        WriteRange(sink, source, nameRange);
        if (valueRange.Start is NoOffset)
        {
            return;
        }

        Utf8StringWriter.Write(sink, "=\""u8);
        WriteRange(sink, source, valueRange);
        Utf8StringWriter.WriteByte(sink, (byte)'"');
    }

    /// <summary>Emits <c>" id=\"...\""</c> using the new id range.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="idRange">Id range.</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void EmitIdAttr(ReadOnlySpan<byte> source, in ByteRange idRange, IBufferWriter<byte> sink)
    {
        Utf8StringWriter.Write(sink, " id=\""u8);
        WriteRange(sink, source, idRange);
        Utf8StringWriter.WriteByte(sink, (byte)'"');
    }

    /// <summary>Emits <c>" class=\"...\""</c> joining <paramref name="existingValue"/> (when non-empty) with <paramref name="classRanges"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="existingValue">Existing class value range (zero-length when no existing class attribute).</param>
    /// <param name="classRanges">New class ranges to append.</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void EmitClassAttr(ReadOnlySpan<byte> source, in ByteRange existingValue, in ReadOnlySpan<ByteRange> classRanges, IBufferWriter<byte> sink)
    {
        Utf8StringWriter.Write(sink, " class=\""u8);
        var wrote = false;
        if (existingValue.Length > 0)
        {
            WriteRange(sink, source, existingValue);
            wrote = true;
        }

        for (var i = 0; i < classRanges.Length; i++)
        {
            if (wrote)
            {
                Utf8StringWriter.WriteByte(sink, (byte)' ');
            }

            WriteRange(sink, source, classRanges[i]);
            wrote = true;
        }

        Utf8StringWriter.WriteByte(sink, (byte)'"');
    }

    /// <summary>Emits <c>" key=\"value\""</c> with attribute-value escape (<c>&amp;</c> / <c>"</c>).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="kv">Key/value range.</param>
    /// <param name="sink">UTF-8 sink.</param>
    private static void EmitKvAttr(ReadOnlySpan<byte> source, in KvRange kv, IBufferWriter<byte> sink)
    {
        Utf8StringWriter.WriteByte(sink, (byte)' ');
        WriteRange(sink, source, kv.Key);
        if (kv.Value.Start is NoOffset)
        {
            return;
        }

        Utf8StringWriter.Write(sink, "=\""u8);
        HtmlEscape.EscapeAttribute(source.Slice(kv.Value.Start, kv.Value.Length), sink);
        Utf8StringWriter.WriteByte(sink, (byte)'"');
    }

    /// <summary>Writes the slice <paramref name="range"/> of <paramref name="source"/> into <paramref name="sink"/>.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="source">Source span.</param>
    /// <param name="range">Range into <paramref name="source"/>.</param>
    private static void WriteRange(IBufferWriter<byte> sink, ReadOnlySpan<byte> source, in ByteRange range)
    {
        if (range.Start is NoOffset || range.Length is 0)
        {
            return;
        }

        Utf8StringWriter.Write(sink, source.Slice(range.Start, range.Length));
    }

    /// <summary>Matches the opener portion (up to and including the colon-or-whitespace after the brace) at or just after <paramref name="p"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="afterOpener">Offset just past the matched opener bytes.</param>
    /// <returns>True when a recognized opener shape was found.</returns>
    private static bool TryMatchOpener(ReadOnlySpan<byte> source, int p, out int afterOpener)
    {
        afterOpener = NoOffset;
        var afterWs = AsciiByteHelpers.SkipWhitespace(source, p);
        if (afterWs + 1 >= source.Length || source[afterWs] is not (byte)'{')
        {
            return false;
        }

        var afterBrace = afterWs + 1;
        var nextByte = source[afterBrace];

        if (nextByte is (byte)':')
        {
            afterOpener = afterBrace + 1;
            return true;
        }

        // {#id …} / {.class …} — python-markdown attr_list shorthand with no leading whitespace.
        if (nextByte is (byte)'#' or (byte)'.')
        {
            afterOpener = afterBrace;
            return true;
        }

        if (!AsciiByteHelpers.IsAsciiWhitespace(nextByte) || !LooksLikeAttrListInner(source, afterBrace))
        {
            return false;
        }

        afterOpener = afterBrace;
        return true;
    }

    /// <summary>Returns the smaller of two offsets, treating -1 as "not found".</summary>
    /// <param name="a">First offset.</param>
    /// <param name="b">Second offset.</param>
    /// <returns>The smaller non-negative offset, or -1 when both are -1.</returns>
    private static int MinNonNegative(int a, int b) =>
        (a, b) switch
        {
            (< 0, < 0) => -1,
            (< 0, _) => b,
            (_, < 0) => a,
            _ => Math.Min(a, b),
        };

    /// <summary>Returns true when the bytes after <paramref name="p"/> look like attr-list inner content.</summary>
    /// <remarks>The lead byte must be one of <c>}</c> (empty marker), <c>.</c>, <c>#</c>, or an attribute-name start byte. Rules out incidental <c>{ </c> usage in code blocks / templates.</remarks>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Offset just past the opening brace (must already be ASCII whitespace).</param>
    /// <returns>True when an attr-list lead byte follows the post-brace whitespace.</returns>
    private static bool LooksLikeAttrListInner(ReadOnlySpan<byte> source, int p)
    {
        var afterInnerWs = AsciiByteHelpers.SkipWhitespace(source, p);
        if (afterInnerWs >= source.Length)
        {
            return false;
        }

        var lead = source[afterInnerWs];
        return lead is (byte)'}' or (byte)'.' or (byte)'#' || IsAttrNameStart(lead);
    }

    /// <summary>Trims trailing whitespace bytes from a range.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Start offset (inclusive).</param>
    /// <param name="end">End offset (exclusive).</param>
    /// <returns>End offset trimmed of trailing whitespace.</returns>
    private static int TrimTrailingWhitespace(ReadOnlySpan<byte> source, int start, int end)
    {
        var p = end;
        while (p > start && AsciiByteHelpers.IsAsciiWhitespace(source[p - 1]))
        {
            p--;
        }

        return p;
    }

    /// <summary>True when <paramref name="b"/> is a valid attribute-name start byte (ASCII letter, underscore, colon).</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when valid.</returns>
    private static bool IsAttrNameStart(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'a' and <= (byte)'z'
            or (byte)'_'
            or (byte)':';

    /// <summary>True when <paramref name="b"/> is a valid attribute-name continuation byte (letters, digits, underscore, colon, dot, dash).</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when valid.</returns>
    private static bool IsAttrNameContinue(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'a' and <= (byte)'z'
            or >= (byte)'0' and <= (byte)'9'
            or (byte)'_'
            or (byte)':'
            or (byte)'.'
            or (byte)'-';

    /// <summary>Stack-local key/value range for one attr-list kv pair — both sides are offsets into the source span.</summary>
    /// <param name="Key">Key range.</param>
    /// <param name="Value">Value range; sentinel start signals a bare flag attribute.</param>
    private readonly record struct KvRange(ByteRange Key, ByteRange Value);

    /// <summary>Bundles the stack-allocated parse buffers and emit-flags for one <c>EmitMerged</c> call so internal helpers stay under the project's parameter cap.</summary>
    /// <remarks>
    /// Plain <c>ref struct</c> rather than <c>record struct</c>: the buffers are <c>stackalloc</c>-backed,
    /// so the type must hold <see cref="Span{T}"/> fields. <see cref="Span{T}"/> may only live inside a
    /// <c>ref struct</c> (CS8345), and the C# grammar disallows the <c>ref</c> modifier on a
    /// <c>record_struct_declaration</c> (CS0106) — so positional record-struct shape is not available here.
    /// Passed by <c>ref</c> so the mutable counters and emit-flag setters propagate to the caller.
    /// </remarks>
    private ref struct AttrListBuffers
    {
        /// <summary>Initializes a new instance of the <see cref="AttrListBuffers"/> struct.</summary>
        /// <param name="classBuffer">Backing storage for parsed <c>.class</c> tokens.</param>
        /// <param name="kvBuffer">Backing storage for parsed <c>key=value</c> pairs.</param>
        /// <param name="kvEmitted">Per-kv "already emitted as override" flag span.</param>
        public AttrListBuffers(in Span<ByteRange> classBuffer, in Span<KvRange> kvBuffer, in Span<bool> kvEmitted)
        {
            ClassBuffer = classBuffer;
            KvBuffer = kvBuffer;
            KvEmitted = kvEmitted;
            IdRange = new(NoOffset, 0);
            ClassCount = 0;
            KvCount = 0;
            IdEmitted = false;
            ClassEmitted = false;
        }

        /// <summary>Gets or sets the parsed <c>#id</c> token range; sentinel start when absent.</summary>
        public ByteRange IdRange { get; set; }

        /// <summary>Gets the backing storage for parsed <c>.class</c> tokens.</summary>
        public Span<ByteRange> ClassBuffer { get; }

        /// <summary>Gets or sets the count of populated entries in <see cref="ClassBuffer"/>.</summary>
        public int ClassCount { get; set; }

        /// <summary>Gets the backing storage for parsed <c>key=value</c> pairs.</summary>
        public Span<KvRange> KvBuffer { get; }

        /// <summary>Gets or sets the count of populated entries in <see cref="KvBuffer"/>.</summary>
        public int KvCount { get; set; }

        /// <summary>Gets the per-kv "already emitted as override" flag span, parallel to <see cref="KvBuffer"/>.</summary>
        public Span<bool> KvEmitted { get; }

        /// <summary>Gets or sets a value indicating whether the id override has already replaced an existing <c>id</c> attribute.</summary>
        public bool IdEmitted { get; set; }

        /// <summary>Gets or sets a value indicating whether the class override has already replaced an existing <c>class</c> attribute.</summary>
        public bool ClassEmitted { get; set; }

        /// <summary>Gets the populated slice of class-token ranges.</summary>
        public readonly ReadOnlySpan<ByteRange> Classes => ClassBuffer[..ClassCount];

        /// <summary>Gets the populated slice of kv-pair ranges.</summary>
        public readonly ReadOnlySpan<KvRange> Kvs => KvBuffer[..KvCount];

        /// <summary>Appends one class-token range, silently dropping when the buffer is full or the range is empty.</summary>
        /// <param name="range">Token range.</param>
        public void AppendClass(in ByteRange range)
        {
            if (range.Length is 0 || ClassCount >= ClassBuffer.Length)
            {
                return;
            }

            ClassBuffer[ClassCount++] = range;
        }

        /// <summary>Appends one kv pair, silently dropping when the key is empty or the buffer is full.</summary>
        /// <param name="key">Key range.</param>
        /// <param name="value">Value range.</param>
        public void AppendKv(in ByteRange key, in ByteRange value)
        {
            if (key.Length is 0 || KvCount >= KvBuffer.Length)
            {
                return;
            }

            KvBuffer[KvCount++] = new(key, value);
        }
    }
}
