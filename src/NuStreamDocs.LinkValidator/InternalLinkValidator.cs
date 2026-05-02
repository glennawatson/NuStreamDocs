// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Walks a <see cref="ValidationCorpus"/> in parallel and reports
/// missing internal pages or unresolved <c>#fragment</c> anchors.
/// </summary>
/// <remarks>
/// Mirrors the surface of <c>mkdocs --strict</c>: relative-link
/// resolution and same-page heading-anchor checking, plus
/// cross-page anchor resolution on top. Path math runs entirely on
/// UTF-8 byte arrays — the source-page URL, link target, and
/// fragment never UTF-16 round-trip during resolution; UTF-8 → string
/// is deferred to the moment a diagnostic message is composed.
/// </remarks>
public static class InternalLinkValidator
{
    /// <summary>Hash byte that separates a link target from its fragment.</summary>
    private const byte HashByte = (byte)'#';

    /// <summary>Forward-slash byte used everywhere as the path separator.</summary>
    private const byte SlashByte = (byte)'/';

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

        var diagnostics = new ConcurrentBag<LinkDiagnostic>();
        var parallelOptions = new ParallelOptions
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

    /// <summary>Validates one page's internal references against the corpus.</summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="page">Page to validate.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ValidatePage(ValidationCorpus corpus, PageLinks page, ConcurrentBag<LinkDiagnostic> sink)
    {
        for (var i = 0; i < page.InternalLinks.Length; i++)
        {
            var link = page.InternalLinks[i];
            if (link is { Length: 0 })
            {
                continue;
            }

            ResolveAndReport(corpus, page, link, sink);
        }
    }

    /// <summary>Resolves one link against the corpus and reports any miss.</summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="source">Source page.</param>
    /// <param name="link">Raw href bytes.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ResolveAndReport(ValidationCorpus corpus, PageLinks source, byte[] link, ConcurrentBag<LinkDiagnostic> sink)
    {
        var span = link.AsSpan();
        var hash = span.IndexOf(HashByte);
        var target = hash < 0 ? span : span[..hash];
        var fragment = hash < 0 ? default : span[(hash + 1)..];

        // Pure same-page anchor: #id
        if (target.IsEmpty)
        {
            if (!fragment.IsEmpty && !ContainsAnchor(source.AnchorIds, fragment))
            {
                sink.Add(BuildDiagnostic(source.PageUrl, link, $"Same-page anchor '#{Encoding.UTF8.GetString(fragment)}' has no matching heading id."));
            }

            return;
        }

        var resolved = ResolveTarget(source.PageUrl, target);
        if (!corpus.TryGetPage(resolved, out var page))
        {
            sink.Add(BuildDiagnostic(source.PageUrl, link, $"Internal link target '{Encoding.UTF8.GetString(resolved)}' is not in the site."));
            return;
        }

        if (fragment.IsEmpty || ContainsAnchor(page.AnchorIds, fragment))
        {
            return;
        }

        sink.Add(BuildDiagnostic(source.PageUrl, link, $"Anchor '#{Encoding.UTF8.GetString(fragment)}' on '{Encoding.UTF8.GetString(resolved)}' has no matching heading id."));
    }

    /// <summary>Looks up a fragment span in <paramref name="anchors"/>.</summary>
    /// <param name="anchors">The anchor-id set.</param>
    /// <param name="fragment">Fragment bytes (no leading <c>#</c>).</param>
    /// <returns>True when the set contains the fragment.</returns>
    /// <remarks>
    /// HashSet lacks a span-keyed lookup so this allocates a byte
    /// array per query; the cost is bounded by the per-page
    /// fragment-link count which is typically near zero.
    /// </remarks>
    private static bool ContainsAnchor(HashSet<byte[]> anchors, ReadOnlySpan<byte> fragment) =>
        anchors.Contains(fragment.ToArray());

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

    /// <summary>Resolves <paramref name="target"/> relative to <paramref name="sourcePage"/>, returning the canonical site-relative bytes.</summary>
    /// <param name="sourcePage">Source page URL bytes.</param>
    /// <param name="target">Target path bytes (no fragment).</param>
    /// <returns>Forward-slashed site-relative URL bytes.</returns>
    private static byte[] ResolveTarget(ReadOnlySpan<byte> sourcePage, ReadOnlySpan<byte> target)
    {
        if (target is [SlashByte, ..])
        {
            return TrimLeadingSlashes(target).ToArray();
        }

        var sourceDirLen = LastSlashIndex(sourcePage);
        if (sourceDirLen < 0)
        {
            return Normalize(target);
        }

        // Compose sourceDir + '/' + target into one buffer for a single Normalize pass.
        var combinedLen = sourceDirLen + 1 + target.Length;
        var combined = new byte[combinedLen];
        sourcePage[..sourceDirLen].CopyTo(combined);
        combined[sourceDirLen] = SlashByte;
        target.CopyTo(combined.AsSpan(sourceDirLen + 1));
        return Normalize(combined);
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

    /// <summary>Collapses <c>./</c> and <c>../</c> segments into a canonical site-relative byte sequence.</summary>
    /// <param name="path">Source path (caller-owned bytes).</param>
    /// <returns>The normalized bytes (always a fresh array).</returns>
    private static byte[] Normalize(ReadOnlySpan<byte> path)
    {
        if (path.IsEmpty)
        {
            return [];
        }

        var segments = new List<(int Start, int Length)>(8);
        CollectSegments(path, segments);
        return segments is []
            ? []
            : JoinSegments(path, segments);
    }

    /// <summary>Walks <paramref name="path"/> and pushes surviving segment ranges into <paramref name="segments"/>, applying <c>.</c> / <c>..</c> rules.</summary>
    /// <param name="path">Source path bytes.</param>
    /// <param name="segments">Accumulator (mutated).</param>
    private static void CollectSegments(ReadOnlySpan<byte> path, List<(int Start, int Length)> segments)
    {
        var cursor = 0;
        while (cursor <= path.Length)
        {
            var rel = path[cursor..].IndexOf(SlashByte);
            var end = rel < 0 ? path.Length : cursor + rel;
            ApplySegment(path, cursor, end - cursor, segments);
            cursor = end + 1;
        }
    }

    /// <summary>Applies a single <c>(start, length)</c> segment to <paramref name="segments"/> per the <c>.</c> / <c>..</c> rules.</summary>
    /// <param name="path">Source path bytes.</param>
    /// <param name="start">Segment start.</param>
    /// <param name="length">Segment length.</param>
    /// <param name="segments">Accumulator (mutated).</param>
    private static void ApplySegment(ReadOnlySpan<byte> path, int start, int length, List<(int Start, int Length)> segments)
    {
        if (length is 0 || path.Slice(start, length).SequenceEqual(Dot))
        {
            return;
        }

        if (path.Slice(start, length).SequenceEqual(DotDot))
        {
            if (segments is [_, ..])
            {
                segments.RemoveAt(segments.Count - 1);
            }

            return;
        }

        segments.Add((start, length));
    }

    /// <summary>Concatenates <paramref name="segments"/> with <c>/</c> separators into a fresh byte array.</summary>
    /// <param name="path">Source path bytes the segments index into.</param>
    /// <param name="segments">Surviving segment ranges.</param>
    /// <returns>The joined bytes.</returns>
    private static byte[] JoinSegments(ReadOnlySpan<byte> path, List<(int Start, int Length)> segments)
    {
        var totalLen = segments.Count - 1;
        for (var i = 0; i < segments.Count; i++)
        {
            totalLen += segments[i].Length;
        }

        var output = new byte[totalLen];
        var write = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                output[write++] = SlashByte;
            }

            var (start, length) = segments[i];
            path.Slice(start, length).CopyTo(output.AsSpan(write, length));
            write += length;
        }

        return output;
    }
}
