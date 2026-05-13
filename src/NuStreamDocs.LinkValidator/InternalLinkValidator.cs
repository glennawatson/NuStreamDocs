// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.LinkValidator;

/// <summary>Walks a <see cref="ValidationCorpus"/> in parallel and reports missing internal pages or unresolved <c>#fragment</c> anchors.</summary>
public static class InternalLinkValidator
{
    /// <summary>Per-page scratch buffer size (bytes) rented from the array pool to materialize resolved targets.</summary>
    private const int ScratchBufferSize = 1024;

    /// <summary>Runs the validator and returns the full diagnostic set.</summary>
    /// <param name="corpus">The pre-built corpus.</param>
    /// <param name="parallelism">Maximum parallel page checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diagnostics in arbitrary order.</returns>
    public static Task<LinkDiagnostic[]> ValidateAsync(
        ValidationCorpus corpus,
        int parallelism,
        CancellationToken cancellationToken) =>
        LinkValidationRun.ForEachPageAsync(corpus, parallelism, ValidatePage, cancellationToken);

    /// <summary>Validates one page's internal references against the corpus.</summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="page">Page to validate.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ValidatePage(ValidationCorpus corpus, PageLinks page, ConcurrentBag<LinkDiagnostic> sink)
    {
        var resolvedScratch = ArrayPool<byte>.Shared.Rent(ScratchBufferSize);
        var combinedScratch = ArrayPool<byte>.Shared.Rent(ScratchBufferSize);
        try
        {
            // Per-page dedup: a target referenced multiple times by different links on the same
            // page should fire exactly one diagnostic. Key is `target` for missing pages and
            // `target#fragment` for missing anchors so the two miss kinds don't collide.
            // Pre-sized to the page's link count so the dedup HashSet never triggers a Resize
            // (which showed up as ~0.23% of total CPU samples in the cross-suite profile).
            PageContext context = new(
                corpus,
                page,
                resolvedScratch,
                combinedScratch,
                sink,
                new(page.InternalLinks.Length, ByteArrayComparer.Instance));

            for (var i = 0; i < page.InternalLinks.Length; i++)
            {
                var link = page.InternalLinks[i];
                if (link is { Length: 0 })
                {
                    continue;
                }

                context.ResolveAndReport(link);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(combinedScratch);
            ArrayPool<byte>.Shared.Return(resolvedScratch);
        }
    }

    /// <summary>Per-page validation state. Bundles the values the resolve / report path
    /// would otherwise pass parameter-by-parameter so the helpers stay narrow.</summary>
    /// <param name="Corpus">Validation corpus.</param>
    /// <param name="Source">Source page being validated.</param>
    /// <param name="ResolvedScratch">Scratch buffer for the resolved (canonical) target bytes.</param>
    /// <param name="CombinedScratch">Scratch buffer for the intermediate <c>sourceDir + '/' + target</c> blob.</param>
    /// <param name="Sink">Diagnostic accumulator.</param>
    /// <param name="Reported">Per-page set of already-reported (target[#fragment]) keys.</param>
    private readonly record struct PageContext(
        ValidationCorpus Corpus,
        PageLinks Source,
        byte[] ResolvedScratch,
        byte[] CombinedScratch,
        ConcurrentBag<LinkDiagnostic> Sink,
        HashSet<byte[]> Reported)
    {
        /// <summary>Hash byte that separates a link target from its fragment.</summary>
        private const byte HashByte = (byte)'#';

        /// <summary>Forward-slash byte used everywhere as the path separator.</summary>
        private const byte SlashByte = (byte)'/';

        /// <summary>Maximum stack-allocated segment-range count before falling back to a pooled rental.</summary>
        private const int StackSegmentLimit = 32;

        /// <summary>Initial pooled-buffer capacity for message assembly; covers every diagnostic shape on this validator without growth.</summary>
        private const int InitialMessageCapacity = 256;

        /// <summary>Gets the two-byte parent-segment marker.</summary>
        private static ReadOnlySpan<byte> DotDot => ".."u8;

        /// <summary>Gets the one-byte current-segment marker.</summary>
        private static ReadOnlySpan<byte> Dot => "."u8;

        /// <summary>Resolves one link against the corpus and reports any miss.</summary>
        /// <param name="link">Raw href bytes.</param>
        public void ResolveAndReport(byte[] link)
        {
            var span = link.AsSpan();

            // Shape pre-check — flag obvious path-construction bugs (e.g. `..//api/...`) with a
            // dedicated diagnostic before we try to resolve them as missing pages. The resolver
            // would still surface a "page not found" miss, but the shape diagnostic explains the
            // root cause (concatenation bug) instead of just the symptom (404).
            if (TryReportMalformedShape(link, span))
            {
                return;
            }

            var hash = span.IndexOf(HashByte);
            var target = hash < 0 ? span : span[..hash];
            var fragment = hash < 0 ? default : span[(hash + 1)..];

            if (target.IsEmpty)
            {
                ReportSamePageAnchor(link, fragment);
                return;
            }

            var written = TryResolveTarget(Source.PageUrl, target, ResolvedScratch, CombinedScratch);
            ReadOnlySpan<byte> resolved = written >= 0
                ? ResolvedScratch.AsSpan(0, written)
                : ResolveTargetOverflow(Source.PageUrl, target);

            if (!Corpus.TryResolvePage(resolved, out var page))
            {
                ReportInternalLinkMiss(link, resolved);
                return;
            }

            if (fragment.IsEmpty || ContainsAnchor(page.AnchorIds, fragment))
            {
                return;
            }

            ReportCrossPageFragment(page, link, resolved, fragment);
        }

        /// <summary>Looks up a fragment span in <paramref name="anchors"/>.</summary>
        /// <param name="anchors">The anchor-id set.</param>
        /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
        /// <returns>True when the set contains the fragment.</returns>
        private static bool ContainsAnchor(HashSet<byte[]> anchors, ReadOnlySpan<byte> fragment) =>
            anchors.Count > 0 && anchors.GetAlternateLookup<ReadOnlySpan<byte>>().Contains(fragment);

        /// <summary>Composes a diagnostic at the byte → ApiCompatString boundary.</summary>
        /// <param name="sourcePageBytes">Source-page URL bytes.</param>
        /// <param name="linkBytes">Raw link bytes.</param>
        /// <param name="messageBytes">Composed message bytes; encoded once into the diagnostic record.</param>
        /// <returns>The diagnostic.</returns>
        private static LinkDiagnostic BuildDiagnostic(
            byte[] sourcePageBytes,
            byte[] linkBytes,
            ReadOnlySpan<byte> messageBytes) =>
            new(
                Encoding.UTF8.GetString(sourcePageBytes),
                Encoding.UTF8.GetString(linkBytes),
                LinkSeverity.Error,
                Encoding.UTF8.GetString(messageBytes));

        /// <summary>Composes a diagnostic from a pre-built <see cref="DiagnosticMessage"/> (used for static-template messages).</summary>
        /// <param name="sourcePageBytes">Source-page URL bytes.</param>
        /// <param name="linkBytes">Raw link bytes.</param>
        /// <param name="message">Pre-built message wrapper.</param>
        /// <returns>The diagnostic.</returns>
        private static LinkDiagnostic BuildDiagnostic(
            byte[] sourcePageBytes,
            byte[] linkBytes,
            DiagnosticMessage message) =>
            new(
                Encoding.UTF8.GetString(sourcePageBytes),
                Encoding.UTF8.GetString(linkBytes),
                LinkSeverity.Error,
                message.ToStringValue());

        /// <summary>Resolves <paramref name="target"/> relative to <paramref name="sourcePage"/> into <paramref name="destination"/> on the happy path with no heap allocation.</summary>
        /// <param name="sourcePage">Source page URL bytes.</param>
        /// <param name="target">Target path bytes (no fragment).</param>
        /// <param name="destination">Caller-owned scratch span; receives the canonical site-relative bytes.</param>
        /// <param name="combinedScratch">Caller-owned scratch buffer for the intermediate <c>sourceDir + '/' + target</c> blob.</param>
        /// <returns>Bytes written into <paramref name="destination"/>, or <c>-1</c> when either scratch is too small (caller falls back to a heap-allocating overflow path).</returns>
        private static int TryResolveTarget(
            ReadOnlySpan<byte> sourcePage,
            ReadOnlySpan<byte> target,
            in Span<byte> destination,
            byte[] combinedScratch)
        {
            if (target is [SlashByte, ..])
            {
                var trimmed = TrimLeadingSlashes(target);
                if (trimmed.Length > destination.Length)
                {
                    return -1;
                }

                trimmed.CopyTo(destination);
                return trimmed.Length;
            }

            var sourceDirLen = LastSlashIndex(sourcePage);
            if (sourceDirLen < 0)
            {
                return Normalize(target, destination);
            }

            var combinedLen = sourceDirLen + 1 + target.Length;
            if (combinedLen > combinedScratch.Length)
            {
                return -1;
            }

            sourcePage[..sourceDirLen].CopyTo(combinedScratch);
            combinedScratch[sourceDirLen] = SlashByte;
            target.CopyTo(combinedScratch.AsSpan(sourceDirLen + 1));
            return Normalize(combinedScratch.AsSpan(0, combinedLen), destination);
        }

        /// <summary>Heap-allocating fallback for the rare case where the resolved target exceeds the per-page scratch buffer.</summary>
        /// <param name="sourcePage">Source page URL bytes.</param>
        /// <param name="target">Target path bytes (no fragment).</param>
        /// <returns>The canonical site-relative bytes.</returns>
        private static byte[] ResolveTargetOverflow(ReadOnlySpan<byte> sourcePage, ReadOnlySpan<byte> target)
        {
            var sourceDirLen = LastSlashIndex(sourcePage);
            var combinedLen = sourceDirLen < 0 ? target.Length : sourceDirLen + 1 + target.Length;
            var heapDest = new byte[combinedLen];
            var heapCombined = new byte[combinedLen];
            var written = TryResolveTarget(sourcePage, target, heapDest, heapCombined);
            if (written < 0)
            {
                // Combined length always upper-bounds the normalized output; defensive.
                throw new InvalidOperationException("Unexpected overflow during link resolution.");
            }

            if (written == heapDest.Length)
            {
                return heapDest;
            }

            var trimmed = new byte[written];
            heapDest.AsSpan(0, written).CopyTo(trimmed);
            return trimmed;
        }

        /// <summary>Returns the index of the last <c>/</c> in <paramref name="path"/>, or <c>-1</c> when none.</summary>
        /// <param name="path">Path bytes.</param>
        /// <returns>Index, or -1.</returns>
        private static int LastSlashIndex(ReadOnlySpan<byte> path) => path.LastIndexOf(SlashByte);

        /// <summary>Strips any leading <c>/</c> bytes.</summary>
        /// <param name="path">Path bytes.</param>
        /// <returns>The trimmed slice.</returns>
        private static ReadOnlySpan<byte> TrimLeadingSlashes(ReadOnlySpan<byte> path)
        {
            var i = 0;
            while (i < path.Length && path[i] is SlashByte)
            {
                i++;
            }

            return path[i..];
        }

        /// <summary>Collapses <c>./</c> and <c>../</c> segments and writes the canonical bytes into <paramref name="destination"/>.</summary>
        /// <param name="path">Source path bytes.</param>
        /// <param name="destination">Caller-owned output span.</param>
        /// <returns>Bytes written, or <c>-1</c> when <paramref name="destination"/> is too small.</returns>
        private static int Normalize(ReadOnlySpan<byte> path, in Span<byte> destination)
        {
            if (path.IsEmpty)
            {
                return 0;
            }

            Span<(int Start, int Length)> segments = stackalloc (int, int)[StackSegmentLimit];
            (int Start, int Length)[]? rented = null;
            var count = 0;

            try
            {
                var cursor = 0;
                while (cursor <= path.Length)
                {
                    var rel = path[cursor..].IndexOf(SlashByte);
                    var end = rel < 0 ? path.Length : cursor + rel;
                    ApplySegment(path, cursor, end - cursor, ref segments, ref count, ref rented);
                    cursor = end + 1;
                }

                if (count is 0)
                {
                    return 0;
                }

                var totalLen = count - 1;
                for (var i = 0; i < count; i++)
                {
                    totalLen += segments[i].Length;
                }

                if (totalLen > destination.Length)
                {
                    return -1;
                }

                var write = 0;
                for (var i = 0; i < count; i++)
                {
                    if (i > 0)
                    {
                        destination[write++] = SlashByte;
                    }

                    var (start, length) = segments[i];
                    path.Slice(start, length).CopyTo(destination[write..]);
                    write += length;
                }

                return write;
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<(int Start, int Length)>.Shared.Return(rented);
                }
            }
        }

        /// <summary>Applies one <c>(start, length)</c> segment to the working segment span per the <c>.</c> / <c>..</c> rules; promotes to a pooled rental when the stack span fills.</summary>
        /// <param name="path">Source path bytes.</param>
        /// <param name="start">Segment start.</param>
        /// <param name="length">Segment length.</param>
        /// <param name="segments">Working segment span (mutated; may be reassigned to a pooled rental).</param>
        /// <param name="count">Live segment count (mutated).</param>
        /// <param name="rented">Pooled rental backing <paramref name="segments"/> once promoted, else <c>null</c>.</param>
        private static void ApplySegment(
            ReadOnlySpan<byte> path,
            int start,
            int length,
            ref Span<(int Start, int Length)> segments,
            ref int count,
            ref (int Start, int Length)[]? rented)
        {
            if (length is 0 || path.Slice(start, length).SequenceEqual(Dot))
            {
                return;
            }

            if (path.Slice(start, length).SequenceEqual(DotDot))
            {
                if (count > 0)
                {
                    count--;
                }

                return;
            }

            if (count == segments.Length)
            {
                var newSize = segments.Length * 2;
                var newRented = ArrayPool<(int Start, int Length)>.Shared.Rent(newSize);
                segments[..count].CopyTo(newRented);
                if (rented is not null)
                {
                    ArrayPool<(int Start, int Length)>.Shared.Return(rented);
                }

                rented = newRented;
                segments = newRented;
            }

            segments[count++] = (start, length);
        }

        /// <summary>Resolves a pure same-page anchor (<c>#id</c>) and emits a diagnostic on miss.</summary>
        /// <param name="link">Raw link bytes (for the diagnostic record).</param>
        /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
        private void ReportSamePageAnchor(byte[] link, ReadOnlySpan<byte> fragment)
        {
            if (fragment.IsEmpty || ContainsAnchor(Source.AnchorIds, fragment))
            {
                return;
            }

            if (ContainsAnchor(Source.DeprecatedNameAnchors, fragment))
            {
                ReportDeprecatedNameAnchor(link, default, fragment);
                return;
            }

            ReportSamePageAnchorMiss(link, fragment);
        }

        /// <summary>
        /// Emits the cross-page-fragment diagnostic, escalating to the HTML4-anchor deprecation message
        /// when the fragment is targeted by an obsolete <c>&lt;a name&gt;</c> on the destination page.
        /// </summary>
        /// <param name="destination">Resolved destination page.</param>
        /// <param name="link">Raw link bytes (for the diagnostic record).</param>
        /// <param name="resolved">Resolved target bytes.</param>
        /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
        private void ReportCrossPageFragment(
            PageLinks destination,
            byte[] link,
            ReadOnlySpan<byte> resolved,
            ReadOnlySpan<byte> fragment)
        {
            if (ContainsAnchor(destination.DeprecatedNameAnchors, fragment))
            {
                ReportDeprecatedNameAnchor(link, resolved, fragment);
                return;
            }

            ReportCrossPageFragmentMiss(link, resolved, fragment);
        }

        /// <summary>Reports a malformed-shape diagnostic when <paramref name="span"/> trips <see cref="MalformedLinkDetector.Inspect"/>.</summary>
        /// <param name="link">Raw link bytes (for the diagnostic record).</param>
        /// <param name="span">The link bytes as a span.</param>
        /// <returns>True when a diagnostic was emitted; the caller short-circuits.</returns>
        private bool TryReportMalformedShape(byte[] link, ReadOnlySpan<byte> span)
        {
            var shape = MalformedLinkDetector.Inspect(span);
            if (shape.IsEmpty)
            {
                return false;
            }

            // Key off the raw link itself — malformed-shape detection runs before resolve.
            ReportShapeMiss(link, span, shape);
            return true;
        }

        /// <summary>Records the (resolved, fragment) dedup key and reports whether the diagnostic is novel.</summary>
        /// <param name="resolved">Resolved target bytes (or empty for same-page anchors).</param>
        /// <param name="fragment">Fragment bytes (or empty when no fragment).</param>
        /// <returns>True when the key was first-seen on this page; false when this exact link was already reported.</returns>
        /// <remarks>
        /// Split from message construction so callers can defer the byte-pipe message build (and
        /// the single boundary <c>GetString</c>) until after dedup — repeated misses on the same
        /// page dominate real corpora and the deferred form drops them to zero formatting cost.
        /// </remarks>
        private bool TryRecordKey(ReadOnlySpan<byte> resolved, ReadOnlySpan<byte> fragment)
        {
            var keyLen = resolved.Length + (fragment.IsEmpty ? 0 : fragment.Length + 1);
            var key = new byte[keyLen];
            resolved.CopyTo(key);
            if (!fragment.IsEmpty)
            {
                key[resolved.Length] = HashByte;
                fragment.CopyTo(key.AsSpan(resolved.Length + 1));
            }

            return Reported.Add(key);
        }

        /// <summary>Reports a missing internal page target.</summary>
        /// <param name="link">Raw link bytes.</param>
        /// <param name="resolved">Resolved (canonical) target bytes.</param>
        private void ReportInternalLinkMiss(byte[] link, ReadOnlySpan<byte> resolved)
        {
            if (!TryRecordKey(resolved, default))
            {
                return;
            }

            using var rental = PageBuilderPool.Rent(InitialMessageCapacity);
            var writer = rental.Writer;
            writer.Write("Internal link target '"u8);
            writer.Write(resolved);
            writer.Write("' is not in the site."u8);
            Sink.Add(BuildDiagnostic(Source.PageUrl, link, writer.WrittenSpan));
        }

        /// <summary>Reports a same-page anchor whose fragment has no matching heading id.</summary>
        /// <param name="link">Raw link bytes.</param>
        /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
        private void ReportSamePageAnchorMiss(byte[] link, ReadOnlySpan<byte> fragment)
        {
            if (!TryRecordKey(default, fragment))
            {
                return;
            }

            using var rental = PageBuilderPool.Rent(InitialMessageCapacity);
            var writer = rental.Writer;
            writer.Write("Same-page anchor '#"u8);
            writer.Write(fragment);
            writer.Write("' has no matching heading id."u8);
            Sink.Add(BuildDiagnostic(Source.PageUrl, link, writer.WrittenSpan));
        }

        /// <summary>Reports a cross-page fragment whose anchor is missing on the destination page.</summary>
        /// <param name="link">Raw link bytes.</param>
        /// <param name="resolved">Resolved destination page bytes.</param>
        /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
        private void ReportCrossPageFragmentMiss(byte[] link, ReadOnlySpan<byte> resolved, ReadOnlySpan<byte> fragment)
        {
            if (!TryRecordKey(resolved, fragment))
            {
                return;
            }

            using var rental = PageBuilderPool.Rent(InitialMessageCapacity);
            var writer = rental.Writer;
            writer.Write("Anchor '#"u8);
            writer.Write(fragment);
            writer.Write("' on '"u8);
            writer.Write(resolved);
            writer.Write("' has no matching heading id."u8);
            Sink.Add(BuildDiagnostic(Source.PageUrl, link, writer.WrittenSpan));
        }

        /// <summary>Reports the HTML4-anchor deprecation guidance for a fragment satisfied only by an obsolete <c>&lt;a name&gt;</c> element.</summary>
        /// <param name="link">Raw link bytes.</param>
        /// <param name="resolved">Resolved destination bytes (empty for same-page targets).</param>
        /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
        private void ReportDeprecatedNameAnchor(byte[] link, ReadOnlySpan<byte> resolved, ReadOnlySpan<byte> fragment)
        {
            if (!TryRecordKey(resolved, fragment))
            {
                return;
            }

            using var rental = PageBuilderPool.Rent(InitialMessageCapacity);
            var writer = rental.Writer;
            writer.Write("Anchor '#"u8);
            writer.Write(fragment);
            writer.Write("' is targeted only by an obsolete HTML4 '<a name=\""u8);
            writer.Write(fragment);
            writer.Write("\">' element. Replace with the HTML5 heading-attribute syntax '## Heading { #"u8);
            writer.Write(fragment);
            writer.Write(
                " }' (or rely on the auto-generated heading id) so the fragment binds to an 'id' attribute."u8);
            Sink.Add(BuildDiagnostic(Source.PageUrl, link, writer.WrittenSpan));
        }

        /// <summary>Reports a malformed-shape diagnostic; <paramref name="shape"/> is a static-template message from <see cref="MalformedLinkDetector"/>.</summary>
        /// <param name="link">Raw link bytes.</param>
        /// <param name="span">Link bytes as a span (used as the dedup key — shape diagnostics fire pre-resolve).</param>
        /// <param name="shape">Pre-built shape message.</param>
        private void ReportShapeMiss(byte[] link, ReadOnlySpan<byte> span, DiagnosticMessage shape)
        {
            if (!TryRecordKey(span, default))
            {
                return;
            }

            Sink.Add(BuildDiagnostic(Source.PageUrl, link, shape));
        }
    }
}
