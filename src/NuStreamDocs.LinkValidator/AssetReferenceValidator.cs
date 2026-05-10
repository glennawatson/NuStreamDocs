// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Walks every page's <see cref="PageLinks.InternalAssets"/> entries (image <c>src</c>, asset
/// <c>href</c>) and reports references whose target file isn't on disk under the site output —
/// the shape that produces broken &lt;img&gt; / &lt;link&gt; / asset-download &lt;a&gt; tags.
/// </summary>
public static class AssetReferenceValidator
{
    /// <summary>Hash byte separating an asset URL from any trailing fragment.</summary>
    private const byte HashByte = (byte)'#';

    /// <summary>Question-mark byte separating an asset URL from any trailing query.</summary>
    private const byte QueryByte = (byte)'?';

    /// <summary>Forward-slash byte used everywhere as the path separator.</summary>
    private const byte SlashByte = (byte)'/';

    /// <summary>Per-page scratch buffer size in bytes for materialized asset paths.</summary>
    private const int ScratchBufferSize = 1024;

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
            MaxDegreeOfParallelism = parallelism,
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

    /// <summary>Walks one page's asset references and reports any miss.</summary>
    /// <param name="corpus">Corpus.</param>
    /// <param name="page">Page being validated.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ValidatePage(ValidationCorpus corpus, PageLinks page, ConcurrentBag<LinkDiagnostic> sink)
    {
        if (page.InternalAssets is [])
        {
            return;
        }

        var scratch = ArrayPool<byte>.Shared.Rent(ScratchBufferSize);
        try
        {
            for (var i = 0; i < page.InternalAssets.Length; i++)
            {
                ResolveAndReport(corpus, page, page.InternalAssets[i], scratch, sink);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    /// <summary>Resolves one asset reference against the corpus and reports any miss.</summary>
    /// <param name="corpus">Corpus.</param>
    /// <param name="source">Source page.</param>
    /// <param name="rawAsset">Raw asset bytes from the page (with optional <c>#</c> / <c>?</c> tail).</param>
    /// <param name="scratch">Caller-owned scratch buffer for the resolved bytes.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ResolveAndReport(
        ValidationCorpus corpus,
        PageLinks source,
        byte[] rawAsset,
        byte[] scratch,
        ConcurrentBag<LinkDiagnostic> sink)
    {
        var span = rawAsset.AsSpan();
        if (span.IsEmpty)
        {
            return;
        }

        var pathSpan = StripTail(span);
        if (pathSpan.IsEmpty)
        {
            return;
        }

        // Site-absolute path (`/images/foo.png`) → strip leading slashes and look up directly.
        if (pathSpan[0] == SlashByte)
        {
            var trimmed = TrimLeadingSlashes(pathSpan);
            if (corpus.ContainsAsset(trimmed))
            {
                return;
            }

            sink.Add(BuildDiagnostic(source.PageUrl, rawAsset, trimmed));
            return;
        }

        // Relative path — join against the source page's directory and normalize `..` / `.`.
        var sourceDir = LastSlashIndex(source.PageUrl);
        var combinedLen = sourceDir + 1 + pathSpan.Length;
        if (combinedLen > scratch.Length)
        {
            ResolveOverflow(corpus, source, rawAsset, pathSpan, sink);
            return;
        }

        var combined = scratch.AsSpan(0, combinedLen);
        if (sourceDir > 0)
        {
            source.PageUrl.AsSpan(0, sourceDir).CopyTo(combined);
            combined[sourceDir] = SlashByte;
            pathSpan.CopyTo(combined[(sourceDir + 1)..]);
        }
        else
        {
            // Source page sits at the site root — the synthetic leading slash + offset is
            // pointless overhead; just point `combined` at `pathSpan` directly.
            combined = scratch.AsSpan(0, pathSpan.Length);
            pathSpan.CopyTo(combined);
        }

        var normalized = scratch.AsSpan(combined.Length, scratch.Length - combined.Length);
        if (normalized.Length < combined.Length)
        {
            ResolveOverflow(corpus, source, rawAsset, pathSpan, sink);
            return;
        }

        var written = Normalize(combined, normalized);
        var resolved = normalized[..written];
        if (corpus.ContainsAsset(resolved))
        {
            return;
        }

        sink.Add(BuildDiagnostic(source.PageUrl, rawAsset, resolved));
    }

    /// <summary>Heap-allocating fallback when the resolved asset path overflows the scratch buffer.</summary>
    /// <param name="corpus">Corpus.</param>
    /// <param name="source">Source page.</param>
    /// <param name="rawAsset">Raw asset bytes.</param>
    /// <param name="pathSpan">Path slice (no fragment / query).</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ResolveOverflow(
        ValidationCorpus corpus,
        PageLinks source,
        byte[] rawAsset,
        ReadOnlySpan<byte> pathSpan,
        ConcurrentBag<LinkDiagnostic> sink)
    {
        var sourceDir = LastSlashIndex(source.PageUrl);
        var combinedLen = sourceDir > 0 ? sourceDir + 1 + pathSpan.Length : pathSpan.Length;
        var combined = new byte[combinedLen];
        if (sourceDir > 0)
        {
            source.PageUrl.AsSpan(0, sourceDir).CopyTo(combined);
            combined[sourceDir] = SlashByte;
            pathSpan.CopyTo(combined.AsSpan(sourceDir + 1));
        }
        else
        {
            pathSpan.CopyTo(combined);
        }

        var normalized = new byte[combinedLen];
        var written = Normalize(combined, normalized);
        var resolved = normalized.AsSpan(0, written);
        if (corpus.ContainsAsset(resolved))
        {
            return;
        }

        sink.Add(BuildDiagnostic(source.PageUrl, rawAsset, resolved));
    }

    /// <summary>Drops <c>#fragment</c> / <c>?query</c> tail from <paramref name="span"/>.</summary>
    /// <param name="span">Raw asset bytes.</param>
    /// <returns>The path-only slice.</returns>
    private static ReadOnlySpan<byte> StripTail(ReadOnlySpan<byte> span)
    {
        var cut = span.IndexOfAny(HashByte, QueryByte);
        return cut < 0 ? span : span[..cut];
    }

    /// <summary>Strips one or more leading <c>/</c> bytes from <paramref name="span"/>.</summary>
    /// <param name="span">Path bytes that may start with one or more <c>/</c>.</param>
    /// <returns>The slice with the leading slashes removed.</returns>
    private static ReadOnlySpan<byte> TrimLeadingSlashes(ReadOnlySpan<byte> span)
    {
        var i = 0;
        while (i < span.Length && span[i] == SlashByte)
        {
            i++;
        }

        return span[i..];
    }

    /// <summary>Returns the offset of the last <c>/</c> in <paramref name="span"/>, or 0 when absent.</summary>
    /// <param name="span">Forward-slashed path bytes.</param>
    /// <returns>Offset of the last separator; 0 for root-level paths.</returns>
    private static int LastSlashIndex(ReadOnlySpan<byte> span)
    {
        var i = span.LastIndexOf(SlashByte);
        return i < 0 ? 0 : i;
    }

    /// <summary>Normalizes <c>..</c> / <c>.</c> path segments into <paramref name="destination"/>.</summary>
    /// <param name="source">Combined path bytes.</param>
    /// <param name="destination">Pre-sized output span.</param>
    /// <returns>Number of bytes written.</returns>
    private static int Normalize(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var written = 0;
        var segmentStart = 0;
        for (var i = 0; i <= source.Length; i++)
        {
            if (i < source.Length && source[i] != SlashByte)
            {
                continue;
            }

            written = ApplySegment(source[segmentStart..i], destination, written);
            segmentStart = i + 1;
        }

        return written;
    }

    /// <summary>Applies one path segment to the normalized output buffer.</summary>
    /// <param name="segment">The segment bytes between separators.</param>
    /// <param name="destination">Output buffer.</param>
    /// <param name="written">Bytes written so far.</param>
    /// <returns>New write position.</returns>
    private static int ApplySegment(ReadOnlySpan<byte> segment, Span<byte> destination, int written)
    {
        if (segment is [(byte)'.', (byte)'.'])
        {
            return PopParent(destination, written);
        }

        if (segment is [(byte)'.'] or [])
        {
            return written;
        }

        if (written > 0)
        {
            destination[written++] = SlashByte;
        }

        segment.CopyTo(destination[written..]);
        return written + segment.Length;
    }

    /// <summary>Walks back one segment + its preceding separator, mirroring the <c>..</c> path semantics.</summary>
    /// <param name="destination">Output buffer.</param>
    /// <param name="written">Bytes written so far.</param>
    /// <returns>New write position.</returns>
    private static int PopParent(Span<byte> destination, int written)
    {
        while (written > 0 && destination[written - 1] != SlashByte)
        {
            written--;
        }

        return written > 0 ? written - 1 : 0;
    }

    /// <summary>Builds the missing-asset diagnostic at the string boundary.</summary>
    /// <param name="sourcePageBytes">Source page URL bytes.</param>
    /// <param name="rawAsset">Original raw asset bytes (for the diagnostic record).</param>
    /// <param name="resolved">Resolved asset path bytes (no fragment / query).</param>
    /// <returns>The diagnostic record.</returns>
    private static LinkDiagnostic BuildDiagnostic(byte[] sourcePageBytes, byte[] rawAsset, ReadOnlySpan<byte> resolved)
    {
        var sourceText = Encoding.UTF8.GetString(sourcePageBytes);
        var resolvedText = Encoding.UTF8.GetString(resolved);
        var rawText = Encoding.UTF8.GetString(rawAsset);
        return new(
            sourceText,
            rawText,
            LinkSeverity.Error,
            $"Local asset target '{resolvedText}' is not on disk under the site output.");
    }
}
