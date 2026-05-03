// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Autorefs.Logging;
using NuStreamDocs.Common;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Rewrites <c>@autoref:ID</c> markers inside emitted HTML to the URL
/// resolved from an <see cref="AutorefsRegistry"/>.
/// </summary>
/// <remarks>
/// The marker syntax is whatever a renderer or theme plugin emits:
/// <c>href="@autoref:System.String"</c> on an anchor, for example.
/// On finalize we walk every <c>.html</c> in the output tree, scan for
/// the prefix, and substitute the resolved URL. Unresolved IDs are
/// left in place — surfacing them in the output makes the missing
/// reference obvious to a reader and to a strict-mode CI gate.
/// </remarks>
public static class AutorefsRewriter
{
    /// <summary>Rewrites every <c>.html</c> file beneath <paramref name="outputRoot"/> in place.</summary>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="registry">Registry to resolve against.</param>
    /// <returns>Number of files rewritten.</returns>
    public static int RewriteAll(string outputRoot, AutorefsRegistry registry)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot);
        ArgumentNullException.ThrowIfNull(registry);

        if (!Directory.Exists(outputRoot) || registry.Count is 0)
        {
            return 0;
        }

        // Materialize the file list once so the parallel pass partitions a stable input.
        var files = Directory.GetFiles(outputRoot, "*.html", SearchOption.AllDirectories);
        var rewritten = 0;

        // Capture-by-ref via local action; Parallel.ForEach passes the file path through as the body input.
        var registryLocal = registry;
        Parallel.ForEach(files, file => Interlocked.Add(ref rewritten, RewriteOne(file, registryLocal) ? 1 : 0));
        return rewritten;
    }

    /// <summary>Rewrites every <c>.html</c> file beneath <paramref name="outputRoot"/> emitting per-reference log events.</summary>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="registry">Registry to resolve against.</param>
    /// <param name="logger">Logger that receives per-reference debug/warning events.</param>
    /// <returns>Counts of resolved and missing references across the site.</returns>
    public static (int Resolved, int Missing) RewriteAll(string outputRoot, AutorefsRegistry registry, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(outputRoot) || registry.Count is 0)
        {
            return (0, 0);
        }

        var files = Directory.GetFiles(outputRoot, "*.html", SearchOption.AllDirectories);
        var resolved = 0;
        var missing = 0;
        var registryLocal = registry;
        var loggerLocal = logger;

        Parallel.ForEach(files, file =>
        {
            var local = default(RewriteTotals);
            RewriteOneLogged(file, registryLocal, loggerLocal, ref local);
            Interlocked.Add(ref resolved, local.Resolved);
            Interlocked.Add(ref missing, local.Missing);
        });

        return (resolved, missing);
    }

    /// <summary>Rewrites a single output file in place.</summary>
    /// <param name="path">Absolute path to the HTML file.</param>
    /// <param name="registry">Registry to resolve against.</param>
    /// <returns>True when the file contained at least one resolvable marker.</returns>
    public static bool RewriteOne(string path, AutorefsRegistry registry)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(registry);

        var source = File.ReadAllBytes(path);
        if (source.AsSpan().IndexOf(AutorefScanner.Marker) < 0)
        {
            return false;
        }

        using var rental = PageBuilderPool.Rent(source.Length);
        var sink = rental.Writer;
        if (!RewriteSpan(source, registry, sink))
        {
            return false;
        }

        File.WriteAllBytes(path, sink.WrittenSpan);
        return true;
    }

    /// <summary>Streams a rewrite of <paramref name="source"/> bytes into <paramref name="sink"/>.</summary>
    /// <param name="source">UTF-8 input.</param>
    /// <param name="registry">Registry to resolve against.</param>
    /// <param name="sink">Writer to receive the rewritten bytes.</param>
    /// <returns>True when at least one marker was substituted.</returns>
    public static bool RewriteSpan(ReadOnlySpan<byte> source, AutorefsRegistry registry, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(sink);

        var totals = default(RewriteTotals);
        return RewriteSpanCore(source, registry, sink, logger: null, sourcePage: null, ref totals);
    }

    /// <summary>Rewrites a single file in place, accumulating resolved/missing counts and emitting log events.</summary>
    /// <param name="path">Absolute path to the HTML file.</param>
    /// <param name="registry">Registry to resolve against.</param>
    /// <param name="logger">Logger for per-reference events.</param>
    /// <param name="totals">Resolved + missing counters threaded across files.</param>
    private static void RewriteOneLogged(string path, AutorefsRegistry registry, ILogger logger, ref RewriteTotals totals)
    {
        var source = File.ReadAllBytes(path);
        if (source.AsSpan().IndexOf(AutorefScanner.Marker) < 0)
        {
            return;
        }

        var sourcePage = Path.GetFileName(path);
        using var rental = PageBuilderPool.Rent(source.Length);
        var sink = rental.Writer;
        if (!RewriteSpanCore(source, registry, sink, logger, sourcePage, ref totals))
        {
            return;
        }

        File.WriteAllBytes(path, sink.WrittenSpan);
    }

    /// <summary>One canonical scan-and-substitute loop; logging fires only when <paramref name="logger"/> is non-null.</summary>
    /// <param name="source">UTF-8 input.</param>
    /// <param name="registry">Registry to resolve against.</param>
    /// <param name="sink">Output sink.</param>
    /// <param name="logger">Optional logger; null disables per-reference events.</param>
    /// <param name="sourcePage">Page identifier surfaced in unresolved warnings; null when logging is disabled.</param>
    /// <param name="totals">Resolved / missing accumulator.</param>
    /// <returns>True when at least one marker was substituted.</returns>
    private static bool RewriteSpanCore(
        ReadOnlySpan<byte> source,
        AutorefsRegistry registry,
        IBufferWriter<byte> sink,
        ILogger? logger,
        string? sourcePage,
        ref RewriteTotals totals)
    {
        var changed = false;
        var cursor = 0;
        while (cursor < source.Length)
        {
            if (!AutorefScanner.TryFindNext(source, cursor, out var match))
            {
                sink.Write(source[cursor..]);
                break;
            }

            sink.Write(source[cursor..match.MarkerStart]);
            changed |= EmitOneMatch(source, in match, registry, sink, logger, sourcePage, ref totals);
            cursor = match.IdEnd;
        }

        return changed;
    }

    /// <summary>Emits one matched <c>@autoref:ID</c> — resolved bytes when the registry hits, or the original marker bytes when it misses.</summary>
    /// <param name="source">UTF-8 input.</param>
    /// <param name="match">Match offsets covering the marker + ID.</param>
    /// <param name="registry">Registry to resolve against.</param>
    /// <param name="sink">Output sink.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="sourcePage">Page identifier for unresolved warnings.</param>
    /// <param name="totals">Resolved / missing accumulator.</param>
    /// <returns>True when the marker resolved.</returns>
    private static bool EmitOneMatch(
        ReadOnlySpan<byte> source,
        in AutorefMatch match,
        AutorefsRegistry registry,
        IBufferWriter<byte> sink,
        ILogger? logger,
        string? sourcePage,
        ref RewriteTotals totals)
    {
        var idLength = match.IdEnd - match.IdStart;
        var idSpan = idLength > 0 ? source.Slice(match.IdStart, idLength) : default;
        if (idLength > 0 && registry.TryResolveInto(idSpan, sink))
        {
            totals.Resolved++;
            if (logger is not null)
            {
                // Logger source-gen wants strings — pay the per-event decode + double-resolve cold path only when
                // a logger is actually attached. The hot path (no logger) stays purely on bytes.
                var idStr = Encoding.UTF8.GetString(idSpan);
                if (registry.TryResolve(idStr, out var urlStr))
                {
                    AutorefsLoggingHelper.LogReferenceResolved(logger, idStr, urlStr);
                }
            }

            return true;
        }

        // Unresolved marker stays verbatim — the missing reference is then visible in the output for diagnosis.
        sink.Write(source[match.MarkerStart..match.IdEnd]);
        totals.Missing++;
        if (logger is null || sourcePage is null)
        {
            return false;
        }

        AutorefsLoggingHelper.LogReferenceUnresolved(logger, Encoding.UTF8.GetString(idSpan), sourcePage);
        return false;
    }

    /// <summary>Per-pass resolved / missing counters threaded through the core loop.</summary>
    /// <param name="Resolved">Resolved-reference accumulator.</param>
    /// <param name="Missing">Unresolved-reference accumulator.</param>
    private record struct RewriteTotals(int Resolved, int Missing);
}
