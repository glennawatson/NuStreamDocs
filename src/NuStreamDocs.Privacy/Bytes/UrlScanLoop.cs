// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Shared scan loop used by every URL-rewrite byte scanner. Walks
/// <c>html</c>, copies through verbatim, and delegates each candidate
/// site to the supplied predicate which either rewrites or advances.
/// </summary>
internal static class UrlScanLoop
{
    /// <summary>Predicate that attempts to rewrite a URL at <paramref name="p"/>; returns true when rewriting occurred.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset up to which bytes have been emitted; updated by the predicate when it writes.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when a rewrite happened.</returns>
    public delegate bool TryRewrite(ReadOnlySpan<byte> html, int p, in UrlRewriteContext ctx, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo);

    /// <summary>Predicate that attempts to record a URL at <paramref name="p"/>; returns true when an audit record was made.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="audit">Audit collector.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when a URL was matched (controls whether scan advances by URL length or one byte).</returns>
    public delegate bool TryAudit(ReadOnlySpan<byte> html, int p, UrlAuditContext audit, out int advanceTo);

    /// <summary>Runs the rewrite scan loop, emitting any unmodified prefix tail when at least one rewrite occurred.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="firstByteSet">Search-values set that filters the byte loop down to candidate offsets.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="probe">Predicate that owns the per-site work.</param>
    /// <returns>True when at least one rewrite happened.</returns>
    public static bool Run(ReadOnlySpan<byte> html, SearchValues<byte> firstByteSet, IBufferWriter<byte> sink, in UrlRewriteContext ctx, TryRewrite probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(sink);

        var changed = false;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOfAny(firstByteSet);
            if (rel < 0)
            {
                break;
            }

            var p = cursor + rel;
            if (probe(html, p, ctx, sink, ref lastEmit, out var advanceTo))
            {
                changed = true;
                cursor = advanceTo;
                continue;
            }

            cursor = advanceTo > p ? advanceTo : p + 1;
        }

        if (!changed)
        {
            return false;
        }

        sink.Write(html[lastEmit..]);
        return true;
    }

    /// <summary>Runs the audit scan loop — no sink, no rewrite, just records URLs into the audit context.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="firstByteSet">Search-values set that filters the byte loop down to candidate offsets.</param>
    /// <param name="audit">Audit context.</param>
    /// <param name="probe">Predicate that owns the per-site work.</param>
    public static void RunAudit(ReadOnlySpan<byte> html, SearchValues<byte> firstByteSet, UrlAuditContext audit, TryAudit probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(audit);

        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOfAny(firstByteSet);
            if (rel < 0)
            {
                break;
            }

            var p = cursor + rel;
            _ = probe(html, p, audit, out var advanceTo);
            cursor = advanceTo > p ? advanceTo : p + 1;
        }
    }
}
