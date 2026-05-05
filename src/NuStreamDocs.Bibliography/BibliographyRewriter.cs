// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Walks markdown source and resolves <c>[@key]</c> markers to their style's
/// in-text reference inline; appends a per-page footnote-definition block
/// and a Bibliography section after the body.
/// </summary>
/// <remarks>
/// Single-pass byte walker — no <c>List&lt;CitationMarker&gt;</c>, no per-marker
/// <c>CitationReference[]</c>, no source <c>byte[]</c> copy. Body bytes are
/// emitted to a pooled <see cref="ArrayBufferWriter{T}"/> rented from
/// <see cref="PageBuilderPool"/>; only when the walk finishes do we flush
/// to the caller's writer (so a page with no resolved cites doesn't
/// disturb the upstream output). Per-cite locator value bytes are copied
/// once into a small <see cref="byte"/> array on capture so the locator
/// outlives the source span without requiring a whole-page <c>ToArray</c>.
/// </remarks>
internal static class BibliographyRewriter
{
    /// <summary>Length of the <c>[@</c> opening sequence.</summary>
    private const int MarkerOpenLength = 2;

    /// <summary>Initial capacity for per-page state buffers (footnote numbers, unique entries).</summary>
    /// <remarks>Real pages cite a handful to a couple of dozen sources; the buffers grow on demand if a page exceeds this cap.</remarks>
    private const int InitialStateCapacity = 16;

    /// <summary>Growth factor applied each time a state buffer overflows.</summary>
    private const int StateGrowthFactor = 2;

    /// <summary>Bytes that may begin a marker — single-byte <see cref="SearchValues{T}"/> keeps the bulk scan vectorized.</summary>
    private static readonly SearchValues<byte> OpenChar = SearchValues.Create("["u8);

    /// <summary>Gets the UTF-8 separator between the original source and the appended footnote / bibliography blocks.</summary>
    private static ReadOnlySpan<byte> SectionBreak => "\n\n"u8;

    /// <summary>Gets the UTF-8 footnote-definition prefix.</summary>
    private static ReadOnlySpan<byte> FootnotePrefix => "[^bib-"u8;

    /// <summary>Gets the UTF-8 bibliography section heading.</summary>
    private static ReadOnlySpan<byte> BibliographyHeading => "## Bibliography\n\n"u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>; emits footnote definitions and a bibliography section after the body.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <param name="database">Resolved citation database.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="missing">Optional callback fired for unresolved keys.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when at least one citation was rewritten.</returns>
    public static bool Rewrite(ReadOnlySpan<byte> source, BibliographyDatabase database, ICitationStyle style, MissingCitationCallback? missing, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(writer);

        if (source.IsEmpty)
        {
            return false;
        }

        using var rental = PageBuilderPool.Rent(source.Length * 2);
        var body = rental.Writer;
        var state = ResolveState.Create(database, style, missing);
        try
        {
            WalkAndEmitBody(source, body, ref state);

            if (state.AssignedCount is 0)
            {
                return false;
            }

            writer.Write(body.WrittenSpan);
            EmitFootnoteDefinitions(state, source, style, writer);
            EmitBibliography(state, style, writer);
            return true;
        }
        finally
        {
            state.ReturnToPool();
        }
    }

    /// <summary>Walks <paramref name="source"/> body bytes, replacing every recognized <c>[@key]</c> marker with the style's in-text references.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="body">Pooled body writer.</param>
    /// <param name="state">Resolution state (mutated as cites resolve).</param>
    private static void WalkAndEmitBody(ReadOnlySpan<byte> source, IBufferWriter<byte> body, ref ResolveState state)
    {
        var cursor = 0;
        while (cursor < source.Length)
        {
            if (TrySkipCodeRegion(source, cursor, out var afterCode))
            {
                Write(body, source[cursor..afterCode]);
                cursor = afterCode;
                continue;
            }

            var rel = source[cursor..].IndexOfAny(OpenChar);
            if (rel < 0)
            {
                Write(body, source[cursor..]);
                return;
            }

            if (rel > 0)
            {
                Write(body, source.Slice(cursor, rel));
                cursor += rel;
            }

            if (TryEmitMarker(source, cursor, body, ref state, out var afterMarker))
            {
                cursor = afterMarker;
                continue;
            }

            // Couldn't parse a marker — emit the bracket literally and resume.
            Write(body, source.Slice(cursor, 1));
            cursor++;
        }
    }

    /// <summary>If the cursor sits at a fenced or inline code region, returns the offset just past it.</summary>
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

    /// <summary>Tries to parse a marker at <paramref name="bracketStart"/> and emit its in-text refs.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="bracketStart">Offset of the leading <c>[</c>.</param>
    /// <param name="body">Body writer.</param>
    /// <param name="state">Resolve state (mutated when cites resolve).</param>
    /// <param name="afterMarker">Offset just past <c>]</c> on success.</param>
    /// <returns>True when a well-formed marker was emitted.</returns>
    private static bool TryEmitMarker(ReadOnlySpan<byte> source, int bracketStart, IBufferWriter<byte> body, ref ResolveState state, out int afterMarker)
    {
        afterMarker = bracketStart;
        if (bracketStart + MarkerOpenLength >= source.Length || source[bracketStart + 1] is not (byte)'@')
        {
            return false;
        }

        var closeRel = source[bracketStart..].IndexOf((byte)']');
        if (closeRel < 0)
        {
            return false;
        }

        var innerStart = bracketStart + 1;
        var inner = source.Slice(innerStart, closeRel - 1);

        // Probe the inner with a temp writer first — if the cite list is malformed we discard everything,
        // and we don't want to commit half a marker to the real body. Probe writer is small and reused
        // per page via a stackalloc-bridged ArrayBufferWriter is overkill, so we just rent on demand.
        using var probeRental = PageBuilderPool.Rent(inner.Length);
        var probe = probeRental.Writer;

        var scan = new CiteScanContext(inner, innerStart, probe);
        if (!TryEmitInner(in scan, ref state, out var resolvedAny))
        {
            // Malformed inner — undo any state we accumulated for this marker before returning.
            state.RollbackTo(state.Snapshot);
            return false;
        }

        if (!resolvedAny)
        {
            // Inner parsed cleanly but no key resolved (all missing) — leave the marker out entirely.
            afterMarker = bracketStart + closeRel + 1;
            return true;
        }

        Write(body, probe.WrittenSpan);
        afterMarker = bracketStart + closeRel + 1;
        return true;
    }

    /// <summary>Parses the inner cite list of one marker and emits in-text refs to <paramref name="scan"/>.<see cref="CiteScanContext.Probe"/>.</summary>
    /// <param name="scan">Bundle of (inner bytes, innerSourceOffset, probe writer) — a <c>ref struct</c> so it can carry <see cref="ReadOnlySpan{T}"/> alongside its sibling fields.</param>
    /// <param name="state">Resolve state.</param>
    /// <param name="resolvedAny">True when at least one cite resolved.</param>
    /// <returns>True when the inner is well-formed (regardless of resolution); false to abort.</returns>
    private static bool TryEmitInner(in CiteScanContext scan, ref ResolveState state, out bool resolvedAny)
    {
        resolvedAny = false;
        state.Snapshot = state.SaveSnapshot();
        var first = true;
        var p = 0;
        while (p < scan.Inner.Length)
        {
            if (!TryConsumeOneCite(in scan, ref p, ref state, ref first, ref resolvedAny, out var separated))
            {
                return false;
            }

            if (!separated && p < scan.Inner.Length)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Consumes one <c>@key[, locator]</c> at <paramref name="p"/> and emits its in-text ref when the key resolves.</summary>
    /// <param name="scan">Inner bytes + source offset + probe writer bundle.</param>
    /// <param name="p">Cursor; advanced past the cite (and any trailing <c>;</c> separator) on a well-formed parse.</param>
    /// <param name="state">Resolve state.</param>
    /// <param name="first">Tracks whether the next emit is the first in this marker; cleared after first successful emit.</param>
    /// <param name="resolvedAny">Set to true when a cite resolves.</param>
    /// <param name="separated">True when a trailing <c>;</c> was consumed (more cites may follow).</param>
    /// <returns>True on a clean parse; false to abort the whole marker.</returns>
    private static bool TryConsumeOneCite(in CiteScanContext scan, ref int p, ref ResolveState state, ref bool first, ref bool resolvedAny, out bool separated)
    {
        separated = false;
        var inner = scan.Inner;
        p = SkipSpaces(inner, p);
        if (p >= inner.Length || inner[p] is not (byte)'@')
        {
            return false;
        }

        p++;
        if (!TryReadCite(inner, scan.InnerSourceOffset, p, out var keyStart, out var keyEnd, out var locator, out var afterCite))
        {
            return false;
        }

        var keySpan = inner.Slice(keyStart, keyEnd - keyStart);
        if (TryAssignCite(ref state, keySpan, locator, out var num, out var entry))
        {
            if (!first)
            {
                Write(scan.Probe, "; "u8);
            }

            state.Style.WriteInText(entry, num, scan.Probe);
            first = false;
            resolvedAny = true;
        }

        p = SkipSpaces(inner, afterCite);
        if (p >= inner.Length || inner[p] is not (byte)';')
        {
            return true;
        }

        p++;
        separated = true;
        return true;
    }

    /// <summary>Reads one <c>key[, locator]</c> at <paramref name="offset"/>.</summary>
    /// <param name="inner">Inner bytes.</param>
    /// <param name="innerSourceOffset">Absolute offset of <paramref name="inner"/>'s first byte within the rewriter's source span.</param>
    /// <param name="offset">Offset just past <c>@</c>.</param>
    /// <param name="keyStart">Inclusive key start (inner-relative).</param>
    /// <param name="keyEnd">Exclusive key end (inner-relative).</param>
    /// <param name="locator">Parsed locator with source-absolute offsets when present.</param>
    /// <param name="afterCite">Offset just past the cite (key or key+locator).</param>
    /// <returns>True on a clean parse.</returns>
    private static bool TryReadCite(ReadOnlySpan<byte> inner, int innerSourceOffset, int offset, out int keyStart, out int keyEnd, out CitationLocator locator, out int afterCite)
    {
        keyStart = offset;
        keyEnd = offset;
        locator = CitationLocator.None;
        afterCite = offset;
        while (keyEnd < inner.Length && IsKeyByte(inner[keyEnd]))
        {
            keyEnd++;
        }

        if (keyEnd == offset)
        {
            return false;
        }

        afterCite = keyEnd;
        var afterKey = SkipSpaces(inner, keyEnd);
        if (afterKey >= inner.Length || inner[afterKey] is not (byte)',')
        {
            return true;
        }

        var afterComma = SkipSpaces(inner, afterKey + 1);
        var locatorEnd = afterComma;
        while (locatorEnd < inner.Length && inner[locatorEnd] is not (byte)';')
        {
            locatorEnd++;
        }

        locator = ParseLocator(inner.Slice(afterComma, locatorEnd - afterComma), innerSourceOffset + afterComma);
        afterCite = locatorEnd;
        return true;
    }

    /// <summary>Resolves <paramref name="keySpan"/> via the database and records the assignment in <paramref name="state"/>.</summary>
    /// <param name="state">Mutable resolve state.</param>
    /// <param name="keySpan">UTF-8 key bytes.</param>
    /// <param name="locator">Parsed locator (already detached from the source span).</param>
    /// <param name="num">Assigned footnote number on success.</param>
    /// <param name="entry">Resolved entry on success.</param>
    /// <returns>True when the key resolved.</returns>
    private static bool TryAssignCite(ref ResolveState state, ReadOnlySpan<byte> keySpan, in CitationLocator locator, out int num, [MaybeNullWhen(false)] out CitationEntry entry)
    {
        if (!state.Database.TryGet(keySpan, out var resolved) || resolved is null)
        {
            state.Missing?.Invoke(Encoding.UTF8.GetString(keySpan));
            num = 0;
            entry = null;
            return false;
        }

        state.AssignedCount++;
        state.EnsureAssignedCapacity();
        state.EntryByNum[state.AssignedCount] = resolved;
        state.LocatorByNum[state.AssignedCount] = locator;
        if (!ContainsByReference(state.Unique, state.UniqueCount, resolved))
        {
            state.EnsureUniqueCapacity();
            state.Unique[state.UniqueCount++] = resolved;
        }

        num = state.AssignedCount;
        entry = resolved;
        return true;
    }

    /// <summary>Splits a locator span into kind + value byte offsets — the resulting offsets are absolute into the rewriter's source span (no per-locator allocation).</summary>
    /// <param name="bytes">Raw locator bytes (may have leading/trailing whitespace).</param>
    /// <param name="bytesSourceOffset">Absolute offset of <paramref name="bytes"/>'s first byte within the rewriter's source span.</param>
    /// <returns>The parsed locator.</returns>
    private static CitationLocator ParseLocator(ReadOnlySpan<byte> bytes, int bytesSourceOffset)
    {
        TrimAsciiBounds(bytes, out var trimStart, out var trimEnd);
        if (trimStart == trimEnd)
        {
            return CitationLocator.None;
        }

        var trimmed = bytes[trimStart..trimEnd];
        var spaceIdx = trimmed.IndexOf((byte)' ');
        if (spaceIdx < 0)
        {
            return new(LocatorKind.None, bytesSourceOffset + trimStart, trimEnd - trimStart);
        }

        var labelSpan = trimmed[..spaceIdx];
        var afterLabel = SkipSpaces(trimmed, spaceIdx + 1);
        var kind = LocatorLabel.Classify(labelSpan);

        // Unrecognized labels (Other) keep the entire trimmed slice — including the original label + space —
        // so the style writes "label value" verbatim. Recognized kinds emit their own canonical prefix and
        // only need the value bytes.
        return kind is LocatorKind.Other
            ? new(kind, bytesSourceOffset + trimStart, trimEnd - trimStart)
            : new(kind, bytesSourceOffset + trimStart + afterLabel, trimEnd - trimStart - afterLabel);
    }

    /// <summary>Computes the trimmed inner bounds of <paramref name="bytes"/> by stepping past leading and trailing ASCII spaces / tabs.</summary>
    /// <param name="bytes">UTF-8 input.</param>
    /// <param name="start">Inclusive trimmed start.</param>
    /// <param name="end">Exclusive trimmed end.</param>
    private static void TrimAsciiBounds(ReadOnlySpan<byte> bytes, out int start, out int end)
    {
        start = 0;
        end = bytes.Length;
        while (start < end && bytes[start] is (byte)' ' or (byte)'\t')
        {
            start++;
        }

        while (end > start && bytes[end - 1] is (byte)' ' or (byte)'\t')
        {
            end--;
        }
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

    /// <summary>Returns true when <paramref name="entry"/> is already in the first <paramref name="count"/> slots of <paramref name="buffer"/>.</summary>
    /// <param name="buffer">Scratch buffer.</param>
    /// <param name="count">Live slot count.</param>
    /// <param name="entry">Candidate entry.</param>
    /// <returns>True when present.</returns>
    private static bool ContainsByReference(CitationEntry[] buffer, int count, CitationEntry entry)
    {
        for (var i = 0; i < count; i++)
        {
            if (ReferenceEquals(buffer[i], entry))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Appends one footnote definition per assigned number — <c>[^bib-id]: rendered citation</c>.</summary>
    /// <param name="state">Resolve state.</param>
    /// <param name="source">Original markdown source span; passed to <see cref="ICitationStyle.WriteFootnote"/> so the style can slice the locator value bytes directly.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitFootnoteDefinitions(ResolveState state, ReadOnlySpan<byte> source, ICitationStyle style, IBufferWriter<byte> writer)
    {
        if (state.AssignedCount is 0)
        {
            return;
        }

        Write(writer, SectionBreak);
        for (var n = 1; n <= state.AssignedCount; n++)
        {
            var entry = state.EntryByNum[n];
            var locator = state.LocatorByNum[n];
            Write(writer, FootnotePrefix);
            Utf8StringWriter.Write(writer, entry.Id);
            Write(writer, "]: "u8);
            style.WriteFootnote(entry, locator, source, writer);
            Write(writer, "\n"u8);
        }
    }

    /// <summary>Appends the Bibliography section listing every unique cited source in citation order.</summary>
    /// <param name="state">Resolve state.</param>
    /// <param name="style">Citation style.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitBibliography(ResolveState state, ICitationStyle style, IBufferWriter<byte> writer)
    {
        if (state.UniqueCount is 0)
        {
            return;
        }

        Write(writer, SectionBreak);
        Write(writer, BibliographyHeading);
        for (var i = 0; i < state.UniqueCount; i++)
        {
            WriteInt(i + 1, writer);
            Write(writer, ". "u8);
            style.WriteBibliography(state.Unique[i], writer);
            Write(writer, "\n"u8);
        }
    }

    /// <summary>Writes an integer as ASCII via <see cref="Utf8Formatter"/>.</summary>
    /// <param name="value">Integer value.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteInt(int value, IBufferWriter<byte> writer)
    {
        Span<byte> buffer = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(value, buffer, out var written))
        {
            return;
        }

        Write(writer, buffer[..written]);
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Single-page citation resolution state — two parallel arrays indexed by 1-based footnote number plus the unique-entry list, all grown on demand.</summary>
    [SuppressMessage(
        "Sonar Code Smell",
        "S3898:Implement IEquatable<T>",
        Justification = "Internal scratch struct; never used as a dictionary key or compared for equality.")]
    private struct ResolveState
    {
        /// <summary>Citation database; supplied by the caller and never mutated.</summary>
        public BibliographyDatabase Database;

        /// <summary>Citation style; supplied by the caller and never mutated.</summary>
        public ICitationStyle Style;

        /// <summary>Optional missing-key callback.</summary>
        public MissingCitationCallback? Missing;

        /// <summary>1-based; slot 0 is unused so the assignment number can stand in for the lookup index without a -1.</summary>
        public CitationEntry[] EntryByNum;

        /// <summary>Parallel to <see cref="EntryByNum"/> — locator at the same footnote number.</summary>
        public CitationLocator[] LocatorByNum;

        /// <summary>Each cited entry once, in first-citation order.</summary>
        public CitationEntry[] Unique;

        /// <summary>Total assigned footnote numbers; <see cref="EntryByNum"/>[1..AssignedCount] is live.</summary>
        public int AssignedCount;

        /// <summary>Live count in <see cref="Unique"/>.</summary>
        public int UniqueCount;

        /// <summary>Snapshot recorded at the start of a marker — used to roll back partial accumulation when an inner parse fails halfway.</summary>
        public ResolveSnapshot Snapshot;

        /// <summary>Initializes a new state with pool-rented buffers pre-sized for a typical page.</summary>
        /// <param name="database">Citation database.</param>
        /// <param name="style">Citation style.</param>
        /// <param name="missing">Missing-key callback.</param>
        /// <returns>Fresh state. The caller must invoke <see cref="ReturnToPool"/> on every exit path to return the rented arrays to <see cref="ArrayPool{T}.Shared"/>.</returns>
        public static ResolveState Create(BibliographyDatabase database, ICitationStyle style, MissingCitationCallback? missing)
        {
            var state = default(ResolveState);
            state.Database = database;
            state.Style = style;
            state.Missing = missing;
            state.EntryByNum = ArrayPool<CitationEntry>.Shared.Rent(InitialStateCapacity + 1);
            state.LocatorByNum = ArrayPool<CitationLocator>.Shared.Rent(InitialStateCapacity + 1);
            state.Unique = ArrayPool<CitationEntry>.Shared.Rent(InitialStateCapacity);
            state.EntryByNum[0] = null!;
            state.LocatorByNum[0] = CitationLocator.None;
            return state;
        }

        /// <summary>Returns every rented buffer to <see cref="ArrayPool{T}.Shared"/> and clears reference slots so the pool doesn't pin stale citation entries or locator byte arrays.</summary>
        public void ReturnToPool()
        {
            if (EntryByNum is not null)
            {
                ArrayPool<CitationEntry>.Shared.Return(EntryByNum, clearArray: true);
                EntryByNum = null!;
            }

            if (LocatorByNum is not null)
            {
                ArrayPool<CitationLocator>.Shared.Return(LocatorByNum, clearArray: true);
                LocatorByNum = null!;
            }

            if (Unique is null)
            {
                return;
            }

            ArrayPool<CitationEntry>.Shared.Return(Unique, clearArray: true);
            Unique = null!;
        }

        /// <summary>Captures the current counts for a roll-back point.</summary>
        /// <returns>Snapshot.</returns>
        public readonly ResolveSnapshot SaveSnapshot() => new(AssignedCount, UniqueCount);

        /// <summary>Rolls counts back to <paramref name="snapshot"/> — drops any cite assignments accumulated since.</summary>
        /// <param name="snapshot">Saved counts.</param>
        public void RollbackTo(in ResolveSnapshot snapshot)
        {
            // Clear any references in the rolled-back range so a subsequent grow doesn't capture a stale entry.
            for (var i = snapshot.AssignedCount + 1; i <= AssignedCount; i++)
            {
                EntryByNum[i] = null!;
                LocatorByNum[i] = CitationLocator.None;
            }

            for (var i = snapshot.UniqueCount; i < UniqueCount; i++)
            {
                Unique[i] = null!;
            }

            AssignedCount = snapshot.AssignedCount;
            UniqueCount = snapshot.UniqueCount;
        }

        /// <summary>Grows <see cref="EntryByNum"/> + <see cref="LocatorByNum"/> via <see cref="ArrayPool{T}.Shared"/> when the next assignment would overflow.</summary>
        public void EnsureAssignedCapacity()
        {
            if (AssignedCount < EntryByNum.Length)
            {
                return;
            }

            var grown = EntryByNum.Length * StateGrowthFactor;
            EntryByNum = GrowPooled(EntryByNum, AssignedCount, grown);
            LocatorByNum = GrowPooled(LocatorByNum, AssignedCount, grown);
        }

        /// <summary>Grows <see cref="Unique"/> via <see cref="ArrayPool{T}.Shared"/> when the next unique would overflow.</summary>
        public void EnsureUniqueCapacity()
        {
            if (UniqueCount < Unique.Length)
            {
                return;
            }

            Unique = GrowPooled(Unique, UniqueCount, Unique.Length * StateGrowthFactor);
        }

        /// <summary>Rents a larger buffer from <see cref="ArrayPool{T}.Shared"/>, copies the live slice into it, and returns the old buffer to the pool.</summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="rented">Current rental.</param>
        /// <param name="liveCount">Number of populated slots to copy.</param>
        /// <param name="minimumLength">Minimum required capacity for the new rental.</param>
        /// <returns>The grown rental.</returns>
        private static T[] GrowPooled<T>(T[] rented, int liveCount, int minimumLength)
        {
            var grown = ArrayPool<T>.Shared.Rent(minimumLength);
            Array.Copy(rented, grown, liveCount);
            ArrayPool<T>.Shared.Return(rented, clearArray: true);
            return grown;
        }
    }

    /// <summary>Snapshot of the live counts in <see cref="ResolveState"/>; used to roll back a marker that turns out to be malformed mid-parse.</summary>
    /// <param name="AssignedCount">Captured <see cref="ResolveState.AssignedCount"/>.</param>
    /// <param name="UniqueCount">Captured <see cref="ResolveState.UniqueCount"/>.</param>
    private readonly record struct ResolveSnapshot(int AssignedCount, int UniqueCount);

    /// <summary>Bundles one marker's parser inputs (inner bytes, source offset, probe writer) so the per-cite walker stays under the project's parameter cap.</summary>
    /// <remarks>
    /// Plain <c>readonly ref struct</c> rather than <c>record struct</c> — record struct cannot hold
    /// <see cref="ReadOnlySpan{T}"/> fields (CS8345) and the C# grammar disallows the <c>ref</c> modifier on
    /// a <c>record_struct_declaration</c> (CS0106), so positional record-struct shape is not available here.
    /// Passed by <c>in</c> at every call site so the bundle never copies.
    /// </remarks>
    private readonly ref struct CiteScanContext
    {
        /// <summary>Initializes a new instance of the <see cref="CiteScanContext"/> struct.</summary>
        /// <param name="inner">Inner bytes between the marker's brackets.</param>
        /// <param name="innerSourceOffset">Absolute offset of <paramref name="inner"/>'s first byte within the rewriter's source span.</param>
        /// <param name="probe">Probe writer that buffers in-text emit for this marker.</param>
        public CiteScanContext(ReadOnlySpan<byte> inner, int innerSourceOffset, IBufferWriter<byte> probe)
        {
            Inner = inner;
            InnerSourceOffset = innerSourceOffset;
            Probe = probe;
        }

        /// <summary>Gets the inner bytes between the marker's brackets.</summary>
        public ReadOnlySpan<byte> Inner { get; }

        /// <summary>Gets the absolute offset of <see cref="Inner"/>'s first byte within the rewriter's source span.</summary>
        public int InnerSourceOffset { get; }

        /// <summary>Gets the probe writer that buffers in-text emit for the current marker.</summary>
        public IBufferWriter<byte> Probe { get; }
    }
}
