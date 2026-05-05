// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Markdown preprocessor that rewrites mkdocs-autorefs reference-link shapes —
/// <c>[shortName][some.anchor.id]</c> — into the project's <c>@autoref:</c>
/// URL marker so the existing finalize-time <see cref="AutorefsRewriter"/>
/// resolves them against the cross-page <see cref="AutorefsRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// Format-agnostic: the label can be any anchor id the registry recognises.
/// SourceDocParser's Zensical emitter writes Roslyn commentIds
/// (<c>T:Foo</c>, <c>M:Foo.Bar(System.Int32)</c>) but the same shape works
/// for any anchor a page declares via <c>[](){#anchor.id}</c>.
/// </para>
/// <para>
/// Reference-style links that resolve through a CommonMark
/// <c>[label]: url</c> definition in the same document are left untouched —
/// the document already supplies the URL, no autoref lookup needed. Tokens
/// inside fenced code blocks and inline code spans are passed through
/// verbatim so prose snippets like <c>`[Foo][T:Bar]`</c> render as code.
/// </para>
/// </remarks>
public static class AutorefsReferenceLinkPreprocessor
{
    /// <summary>Length of the autoref marker prefix written into the rewritten link target.</summary>
    private const int AutorefMarkerPrefixLength = 9;

    /// <summary>Number of fence-marker bytes (<c>```</c>) that flip a code-fence state.</summary>
    private const int FenceMarkerLength = 3;

    /// <summary>Maximum byte count we allow for a label inside <c>[ ]</c> — keeps the scan bounded against pathological inputs.</summary>
    private const int MaxLabelBytes = 256;

    /// <summary>Gets the UTF-8 prefix of the rewritten URL target.</summary>
    private static ReadOnlySpan<byte> AutorefPrefix => "@autoref:"u8;

    /// <summary>Returns true when <paramref name="source"/> contains a <c>][</c> sequence — the cheapest signal that a reference-style link might be present.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <returns>True when at least one <c>][</c> appears; false short-circuits the rewriter.</returns>
    public static bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        source.IndexOf("]["u8) >= 0;

    /// <summary>
    /// Rewrites every <c>[text][label]</c> in <paramref name="source"/> whose label isn't a
    /// defined CommonMark link reference into <c>[text](@autoref:label)</c>. Everything
    /// else passes through verbatim.
    /// </summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var definedLabels = CollectLinkDefinitions(source);
        ScanState state = default;

        // The fence-toggle path triggers on '\n' so a fence on the first line of the
        // file isn't seen by the per-byte scan. Detect a leading fence up-front.
        if (TryAdvancePastFenceMarker(source, ref state.Cursor))
        {
            state.InFence = true;
        }

        while (state.Cursor < source.Length)
        {
            StepRewriter(source, definedLabels, writer, ref state);
        }

        FlushPreservedRun(source, state.LastEmitted, source.Length, writer);
    }

    /// <summary>One iteration of the main rewriter loop — advances <paramref name="state"/> past the next byte, code span, fence transition, or matched reference link.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="definedLabels">Labels that already resolve to <c>[label]: url</c> definitions.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="state">Mutable scan state.</param>
    private static void StepRewriter(
        ReadOnlySpan<byte> source,
        HashSet<string> definedLabels,
        IBufferWriter<byte> writer,
        ref ScanState state)
    {
        var b = source[state.Cursor];
        if (b is (byte)'\n')
        {
            state.Cursor++;
            if (TryAdvancePastFenceMarker(source, ref state.Cursor))
            {
                state.InFence = !state.InFence;
            }

            return;
        }

        if (state.InFence)
        {
            state.Cursor++;
            return;
        }

        switch (b)
        {
            case (byte)'`':
                {
                    SkipInlineCodeSpan(source, ref state.Cursor);
                    return;
                }

            case (byte)'[' when TryConsumeReferenceLink(source, definedLabels, writer, ref state):
                return;
            default:
                {
                    state.Cursor++;
                    break;
                }
        }
    }

    /// <summary>Attempts to consume a <c>[text][label]</c> sequence at the current cursor; rewrites it when the label has no document-level definition.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="definedLabels">Labels resolved by inline <c>[label]: url</c> definitions.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="state">Mutable scan state.</param>
    /// <returns>True when the cursor advanced past a complete reference-link shape.</returns>
    private static bool TryConsumeReferenceLink(
        ReadOnlySpan<byte> source,
        HashSet<string> definedLabels,
        IBufferWriter<byte> writer,
        ref ScanState state)
    {
        if (!TryMatchReferenceLink(
                source,
                state.Cursor,
                out var labelStart,
                out var labelEnd,
                out var idStart,
                out var idEnd))
        {
            return false;
        }

        if (!IsDefinedLabel(definedLabels, source, idStart, idEnd))
        {
            FlushPreservedRun(source, state.LastEmitted, state.Cursor, writer);
            EmitRewrittenReference(source, labelStart, labelEnd, idStart, idEnd, writer);
            state.Cursor = idEnd + 1;
            state.LastEmitted = state.Cursor;
            return true;
        }

        state.Cursor = idEnd + 1;
        return true;
    }

    /// <summary>
    /// Scans <paramref name="source"/> for line-anchored <c>[label]: url</c> link definitions;
    /// the returned set is used to skip rewriting references whose label is already
    /// resolved by the document.
    /// </summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <returns>Case-sensitive set of defined label byte sequences.</returns>
    private static HashSet<string> CollectLinkDefinitions(ReadOnlySpan<byte> source)
    {
        HashSet<string> labels = new(StringComparer.Ordinal);
        var cursor = 0;
        while (cursor < source.Length)
        {
            while (cursor < source.Length && source[cursor] is (byte)' ' or (byte)'\t')
            {
                cursor++;
            }

            if (cursor >= source.Length || source[cursor] is not (byte)'[')
            {
                cursor = AdvanceToNextLineStart(source, cursor);
                continue;
            }

            var labelStart = cursor + 1;
            if (!TryFindLabelClose(source, labelStart, out var labelEnd))
            {
                cursor = AdvanceToNextLineStart(source, cursor);
                continue;
            }

            var afterLabel = labelEnd + 1;
            if (afterLabel >= source.Length || source[afterLabel] is not (byte)':')
            {
                cursor = AdvanceToNextLineStart(source, cursor);
                continue;
            }

            labels.Add(Encoding.UTF8.GetString(source.Slice(labelStart, labelEnd - labelStart)));
            cursor = AdvanceToNextLineStart(source, afterLabel);
        }

        return labels;
    }

    /// <summary>Steps <paramref name="cursor"/> to the start of the next line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Starting cursor.</param>
    /// <returns>Index just past the next <c>\n</c>, or <paramref name="source"/>.Length when none remains.</returns>
    private static int AdvanceToNextLineStart(ReadOnlySpan<byte> source, int cursor)
    {
        while (cursor < source.Length && source[cursor] is not (byte)'\n')
        {
            cursor++;
        }

        return cursor < source.Length ? cursor + 1 : cursor;
    }

    /// <summary>
    /// Returns true when the bytes between <paramref name="idStart"/> and <paramref name="idEnd"/>
    /// match a label declared by a CommonMark <c>[label]: url</c> definition.
    /// </summary>
    /// <param name="defined">Definition set.</param>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="idStart">Inclusive start of the candidate label.</param>
    /// <param name="idEnd">Exclusive end of the candidate label.</param>
    /// <returns>True when the label is already defined.</returns>
    private static bool IsDefinedLabel(HashSet<string> defined, ReadOnlySpan<byte> source, int idStart, int idEnd) =>
        defined.Count is not 0 && defined.Contains(Encoding.UTF8.GetString(source.Slice(idStart, idEnd - idStart)));

    /// <summary>Tries to match a complete <c>[label][id]</c> structure starting at <paramref name="bracketIndex"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="bracketIndex">Index of the opening <c>[</c>.</param>
    /// <param name="labelStart">Inclusive start of the label content.</param>
    /// <param name="labelEnd">Exclusive end of the label content.</param>
    /// <param name="idStart">Inclusive start of the id content.</param>
    /// <param name="idEnd">Exclusive end of the id content.</param>
    /// <returns>True on a complete match.</returns>
    private static bool TryMatchReferenceLink(
        ReadOnlySpan<byte> source,
        int bracketIndex,
        out int labelStart,
        out int labelEnd,
        out int idStart,
        out int idEnd)
    {
        idStart = -1;
        idEnd = -1;
        labelStart = bracketIndex + 1;
        if (!TryFindLabelClose(source, labelStart, out labelEnd))
        {
            return false;
        }

        var afterLabel = labelEnd + 1;
        if (afterLabel >= source.Length || source[afterLabel] is not (byte)'[')
        {
            return false;
        }

        idStart = afterLabel + 1;
        return TryFindLabelClose(source, idStart, out idEnd) && idEnd > idStart;
    }

    /// <summary>Walks <paramref name="source"/> from <paramref name="start"/> looking for the closing <c>]</c> on the same line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Inclusive start (just past the opening <c>[</c>).</param>
    /// <param name="end">Index of the closing <c>]</c> on success.</param>
    /// <returns>True when a non-empty bracketed label is found before EOL or a nested <c>[</c>.</returns>
    private static bool TryFindLabelClose(ReadOnlySpan<byte> source, int start, out int end)
    {
        end = -1;
        var maxIndex = Math.Min(source.Length, start + MaxLabelBytes);
        for (var i = start; i < maxIndex; i++)
        {
            switch (source[i])
            {
                case (byte)']':
                    {
                        end = i;
                        return i > start;
                    }

                case (byte)'\n' or (byte)'[':
                    return false;
            }
        }

        return false;
    }

    /// <summary>Emits <c>[label](@autoref:id)</c> for the matched reference link.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="labelStart">Inclusive label start.</param>
    /// <param name="labelEnd">Exclusive label end.</param>
    /// <param name="idStart">Inclusive id start.</param>
    /// <param name="idEnd">Exclusive id end.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitRewrittenReference(
        ReadOnlySpan<byte> source,
        int labelStart,
        int labelEnd,
        int idStart,
        int idEnd,
        IBufferWriter<byte> writer)
    {
        var labelLen = labelEnd - labelStart;
        var idLen = idEnd - idStart;
        var totalLen = 1 + labelLen + 2 + AutorefMarkerPrefixLength + idLen + 1;
        var dst = writer.GetSpan(totalLen);
        var pos = 0;
        dst[pos++] = (byte)'[';
        source.Slice(labelStart, labelLen).CopyTo(dst[pos..]);
        pos += labelLen;
        dst[pos++] = (byte)']';
        dst[pos++] = (byte)'(';
        AutorefPrefix.CopyTo(dst[pos..]);
        pos += AutorefMarkerPrefixLength;
        source.Slice(idStart, idLen).CopyTo(dst[pos..]);
        pos += idLen;
        dst[pos++] = (byte)')';
        writer.Advance(pos);
    }

    /// <summary>Copies the verbatim run between <paramref name="from"/> and <paramref name="to"/> to <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="from">Inclusive start.</param>
    /// <param name="to">Exclusive end.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void FlushPreservedRun(ReadOnlySpan<byte> source, int from, int to, IBufferWriter<byte> writer)
    {
        if (to <= from)
        {
            return;
        }

        var len = to - from;
        var dst = writer.GetSpan(len);
        source.Slice(from, len).CopyTo(dst);
        writer.Advance(len);
    }

    /// <summary>
    /// Skips an inline code span starting at <paramref name="cursor"/>; advances
    /// <paramref name="cursor"/> past the closing run of backticks (or to
    /// end-of-input on a malformed span).
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Cursor; advanced past the entire span.</param>
    private static void SkipInlineCodeSpan(ReadOnlySpan<byte> source, ref int cursor)
    {
        var openTicks = 0;
        while (cursor < source.Length && source[cursor] is (byte)'`')
        {
            openTicks++;
            cursor++;
        }

        while (cursor < source.Length)
        {
            switch (source[cursor])
            {
                case (byte)'\n':
                    {
                        cursor++;
                        return;
                    }

                case (byte)'`':
                    {
                        var closeStart = cursor;
                        while (cursor < source.Length && source[cursor] is (byte)'`')
                        {
                            cursor++;
                        }

                        if (cursor - closeStart == openTicks)
                        {
                            return;
                        }

                        break;
                    }

                default:
                    {
                        cursor++;
                        break;
                    }
            }
        }
    }

    /// <summary>Tries to step <paramref name="cursor"/> past a fence-line marker (<c>```</c> or <c>~~~</c>) preceded by optional spaces / tabs.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Cursor; positioned just after a newline.</param>
    /// <returns>True when a fence marker was consumed.</returns>
    private static bool TryAdvancePastFenceMarker(ReadOnlySpan<byte> source, ref int cursor)
    {
        var probe = cursor;
        while (probe < source.Length && source[probe] is (byte)' ' or (byte)'\t')
        {
            probe++;
        }

        if (probe + FenceMarkerLength > source.Length)
        {
            return false;
        }

        var fenceByte = source[probe];
        if (fenceByte is not ((byte)'`' or (byte)'~'))
        {
            return false;
        }

        for (var k = 1; k < FenceMarkerLength; k++)
        {
            if (source[probe + k] != fenceByte)
            {
                return false;
            }
        }

        cursor = probe + FenceMarkerLength;
        return true;
    }

    /// <summary>Mutable cursor + emit-pointer + fence flag bundle threaded through the rewriter loop.</summary>
    /// <remarks>
    /// Record struct (no positional constructor) — gets <see cref="IEquatable{T}"/> +
    /// hash + equality for free, while keeping fields mutable so the rewriter can
    /// do <c>state.Cursor++</c> in place.
    /// </remarks>
    private record struct ScanState
    {
        /// <summary>Current scan offset.</summary>
        public int Cursor;

        /// <summary>Offset up to which the verbatim run has been flushed to the sink.</summary>
        public int LastEmitted;

        /// <summary>True while inside a fenced code block.</summary>
        public bool InFence;
    }
}
