// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;

namespace NuStreamDocs.LinkValidator;

/// <summary>Walks a <see cref="ValidationCorpus"/> in parallel and reports missing internal pages or unresolved <c>#fragment</c> anchors.</summary>
public static class InternalLinkValidator
{
    /// <summary>Hash byte that separates a link target from its fragment.</summary>
    private const byte HashByte = (byte)'#';

    /// <summary>Forward-slash byte used everywhere as the path separator.</summary>
    private const byte SlashByte = (byte)'/';

    /// <summary>Per-page scratch buffer size (bytes) rented from the array pool to materialize resolved targets.</summary>
    private const int ScratchBufferSize = 1024;

    /// <summary>Maximum stack-allocated segment-range count before falling back to a pooled rental.</summary>
    private const int StackSegmentLimit = 32;

    /// <summary>Gets the two-byte parent-segment marker.</summary>
    private static ReadOnlySpan<byte> DotDot => ".."u8;

    /// <summary>Gets the one-byte current-segment marker.</summary>
    private static ReadOnlySpan<byte> Dot => "."u8;

    /// <summary>Runs the validator and returns the full diagnostic set.</summary>
    /// <param name="corpus">The pre-built corpus.</param>
    /// <param name="parallelism">Maximum parallel page checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diagnostics in arbitrary order.</returns>
    public static async Task<LinkDiagnostic[]> ValidateAsync(ValidationCorpus corpus, int parallelism, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);

        ConcurrentBag<LinkDiagnostic> diagnostics = [];
        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = parallelism
        };

        await Parallel.ForEachAsync(
            corpus.Pages,
            parallelOptions,
            (page, _) =>
            {
                ValidatePage(corpus, page, diagnostics);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        return [.. diagnostics];
    }

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
            for (var i = 0; i < page.InternalLinks.Length; i++)
            {
                var link = page.InternalLinks[i];
                if (link is { Length: 0 })
                {
                    continue;
                }

                ResolveAndReport(corpus, page, link, resolvedScratch, combinedScratch, sink);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(combinedScratch);
            ArrayPool<byte>.Shared.Return(resolvedScratch);
        }
    }

    /// <summary>Resolves one link against the corpus and reports any miss.</summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="source">Source page.</param>
    /// <param name="link">Raw href bytes.</param>
    /// <param name="resolvedScratch">Caller-owned scratch buffer for the resolved target bytes.</param>
    /// <param name="combinedScratch">Caller-owned scratch buffer for the intermediate <c>sourceDir + '/' + target</c> blob.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ResolveAndReport(
        ValidationCorpus corpus,
        PageLinks source,
        byte[] link,
        byte[] resolvedScratch,
        byte[] combinedScratch,
        ConcurrentBag<LinkDiagnostic> sink)
    {
        var span = link.AsSpan();
        var hash = span.IndexOf(HashByte);
        var target = hash < 0 ? span : span[..hash];
        var fragment = hash < 0 ? default : span[(hash + 1)..];

        // Pure same-page anchor: #id
        if (target.IsEmpty)
        {
            if (fragment.IsEmpty || ContainsAnchor(source.AnchorIds, fragment))
            {
                return;
            }

            sink.Add(BuildSamePageFragmentDiagnostic(source, link, fragment));
            return;
        }

        var written = TryResolveTarget(source.PageUrl, target, resolvedScratch, combinedScratch);
        ReadOnlySpan<byte> resolved = written >= 0
            ? resolvedScratch.AsSpan(0, written)
            : ResolveTargetOverflow(source.PageUrl, target);

        if (!corpus.TryResolvePage(resolved, out var page))
        {
            sink.Add(BuildDiagnostic(source.PageUrl, link, $"Internal link target '{Encoding.UTF8.GetString(resolved)}' is not in the site."));
            return;
        }

        if (fragment.IsEmpty || ContainsAnchor(page.AnchorIds, fragment))
        {
            return;
        }

        sink.Add(BuildCrossPageFragmentDiagnostic(page, source.PageUrl, link, resolved, fragment));
    }

    /// <summary>
    /// Builds a same-page-fragment diagnostic, escalating to the HTML4-anchor deprecation message
    /// when the fragment is targeted by an obsolete <c>&lt;a name&gt;</c> on the source page.
    /// </summary>
    /// <param name="source">Source page.</param>
    /// <param name="link">Raw link bytes (for the diagnostic record).</param>
    /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
    /// <returns>The diagnostic to record.</returns>
    private static LinkDiagnostic BuildSamePageFragmentDiagnostic(PageLinks source, byte[] link, ReadOnlySpan<byte> fragment)
    {
        if (ContainsAnchor(source.DeprecatedNameAnchors, fragment))
        {
            return BuildDiagnostic(source.PageUrl, link, BuildDeprecatedNameAnchorMessage(fragment));
        }

        var fragText = Encoding.UTF8.GetString(fragment);
        return BuildDiagnostic(source.PageUrl, link, $"Same-page anchor '#{fragText}' has no matching heading id.");
    }

    /// <summary>
    /// Builds a cross-page-fragment diagnostic, escalating to the HTML4-anchor deprecation message
    /// when the fragment is targeted by an obsolete <c>&lt;a name&gt;</c> on the destination page.
    /// </summary>
    /// <param name="destination">Resolved destination page.</param>
    /// <param name="sourcePage">Source page URL bytes.</param>
    /// <param name="link">Raw link bytes (for the diagnostic record).</param>
    /// <param name="resolved">Resolved target bytes.</param>
    /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
    /// <returns>The diagnostic to record.</returns>
    private static LinkDiagnostic BuildCrossPageFragmentDiagnostic(PageLinks destination, byte[] sourcePage, byte[] link, ReadOnlySpan<byte> resolved, ReadOnlySpan<byte> fragment)
    {
        if (ContainsAnchor(destination.DeprecatedNameAnchors, fragment))
        {
            return BuildDiagnostic(sourcePage, link, BuildDeprecatedNameAnchorMessage(fragment));
        }

        var fragText = Encoding.UTF8.GetString(fragment);
        var pageText = Encoding.UTF8.GetString(resolved);
        return BuildDiagnostic(sourcePage, link, $"Anchor '#{fragText}' on '{pageText}' has no matching heading id.");
    }

    /// <summary>Composes the deprecation guidance message for a fragment satisfied only by an obsolete <c>&lt;a name&gt;</c> anchor.</summary>
    /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
    /// <returns>Message string to record on the diagnostic.</returns>
    private static string BuildDeprecatedNameAnchorMessage(ReadOnlySpan<byte> fragment)
    {
        var name = Encoding.UTF8.GetString(fragment);
        return $"Anchor '#{name}' is targeted only by an obsolete HTML4 '<a name=\"{name}\">' element. "
             + $"Replace with the HTML5 heading-attribute syntax '## Heading {{ #{name} }}' "
             + "(or rely on the auto-generated heading id) so the fragment binds to an 'id' attribute.";
    }

    /// <summary>Looks up a fragment span in <paramref name="anchors"/>.</summary>
    /// <param name="anchors">The anchor-id set.</param>
    /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
    /// <returns>True when the set contains the fragment.</returns>
    private static bool ContainsAnchor(HashSet<byte[]> anchors, ReadOnlySpan<byte> fragment) =>
        anchors.Count > 0 && anchors.GetAlternateLookup<ReadOnlySpan<byte>>().Contains(fragment);

    /// <summary>Composes a diagnostic at the string boundary.</summary>
    /// <param name="sourcePageBytes">Source-page URL bytes.</param>
    /// <param name="linkBytes">Raw link bytes.</param>
    /// <param name="message">Composed message.</param>
    /// <returns>The diagnostic.</returns>
    private static LinkDiagnostic BuildDiagnostic(byte[] sourcePageBytes, byte[] linkBytes, string message) =>
        new(
            Encoding.UTF8.GetString(sourcePageBytes),
            Encoding.UTF8.GetString(linkBytes),
            LinkSeverity.Error,
            message);

    /// <summary>Resolves <paramref name="target"/> relative to <paramref name="sourcePage"/> into <paramref name="destination"/> on the happy path with no heap allocation.</summary>
    /// <param name="sourcePage">Source page URL bytes.</param>
    /// <param name="target">Target path bytes (no fragment).</param>
    /// <param name="destination">Caller-owned scratch span; receives the canonical site-relative bytes.</param>
    /// <param name="combinedScratch">Caller-owned scratch buffer for the intermediate <c>sourceDir + '/' + target</c> blob.</param>
    /// <returns>Bytes written into <paramref name="destination"/>, or <c>-1</c> when either scratch is too small (caller falls back to a heap-allocating overflow path).</returns>
    private static int TryResolveTarget(ReadOnlySpan<byte> sourcePage, ReadOnlySpan<byte> target, Span<byte> destination, byte[] combinedScratch)
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
    private static int Normalize(ReadOnlySpan<byte> path, Span<byte> destination)
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
}
