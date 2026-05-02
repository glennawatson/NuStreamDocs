// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Stateless HTML rewriter that finds <c>http(s)://</c> URLs in
/// asset-bearing attributes and rewrites them to local paths obtained
/// from an <see cref="ExternalAssetRegistry"/>.
/// </summary>
internal static class ExternalUrlScanner
{
    /// <summary>Delegate matching the byte-level rewriter signature.</summary>
    /// <param name="source">UTF-8 source span.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when the stage rewrote at least one URL.</returns>
    private delegate bool ByteStage(ReadOnlySpan<byte> source, in UrlRewriteContext ctx, IBufferWriter<byte> sink);

    /// <summary>Returns true when <paramref name="html"/> may contain an external <c>http(s)://</c> reference.</summary>
    /// <param name="html">Page HTML.</param>
    /// <returns>True when the cheap pre-filter matches.</returns>
    public static bool MayHaveExternalUrls(ReadOnlySpan<byte> html) =>
        html.IndexOf("http"u8) >= 0;

    /// <summary>Records every external URL <see cref="Rewrite"/> would have localized, without modifying <paramref name="html"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="filter">Host filter.</param>
    /// <param name="auditSet">Concurrent byte-array-keyed set the URLs are added to (the value is unused).</param>
    public static void Audit(ReadOnlySpan<byte> html, HostFilter filter, ConcurrentDictionary<byte[], byte> auditSet)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(auditSet);
        var ctx = new UrlAuditContext(filter, auditSet);
        AssetAttributeBytes.AuditInto(html, ctx);
        SrcsetBytes.AuditInto(html, ctx);
        CssUrlBytes.AuditInto(html, ctx);
    }

    /// <summary>Rewrites every external URL found in <c>src</c>/<c>href</c>/<c>srcset</c> attributes plus inline <c>&lt;style&gt;</c> bodies of <paramref name="html"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="registry">URL registry; entries created on demand.</param>
    /// <param name="filter">Per-URL filter encapsulating the allow/skip lists.</param>
    /// <returns>The rewritten HTML.</returns>
    public static byte[] Rewrite(ReadOnlySpan<byte> html, ExternalAssetRegistry registry, HostFilter filter)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(filter);

        var ctx = new UrlRewriteContext(filter, registry);
        return RunStages(html, ctx);
    }

    /// <summary>Public string-shaped variant kept for API compatibility.</summary>
    /// <param name="input">Page HTML as a string.</param>
    /// <param name="registry">URL registry.</param>
    /// <param name="filter">Host filter.</param>
    /// <returns>Rewritten HTML; the same instance when nothing matched.</returns>
    public static string RewriteString(string input, ExternalAssetRegistry registry, HostFilter filter)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(filter);

        var bytes = Encoding.UTF8.GetBytes(input);
        var ctx = new UrlRewriteContext(filter, registry);
        var rewritten = RunStages(bytes, ctx);
        return ReferenceEquals(rewritten, bytes) ? input : Encoding.UTF8.GetString(rewritten);
    }

    /// <summary>Runs all three rewrite stages, threading the buffer through without copying when no stage rewrites.</summary>
    /// <param name="html">Initial UTF-8 buffer (may be borrowed from a span).</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <returns>The final byte buffer; equal-by-reference to the input when nothing changed.</returns>
    private static byte[] RunStages(ReadOnlySpan<byte> html, in UrlRewriteContext ctx)
    {
        var current = html.ToArray();
        current = RunOne(current, ctx, AssetAttributeBytes.RewriteInto);
        current = RunOne(current, ctx, SrcsetBytes.RewriteInto);
        current = RunOne(current, ctx, InlineStyleBlockBytes.RewriteInto);
        return current;
    }

    /// <summary>Runs one stage; returns the same buffer reference when no rewrite happened, a fresh array otherwise.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="stage">Stage to run.</param>
    /// <returns>Output buffer (same reference as <paramref name="source"/> when unchanged).</returns>
    private static byte[] RunOne(byte[] source, in UrlRewriteContext ctx, ByteStage stage)
    {
        var sink = new ArrayBufferWriter<byte>(source.Length);
        return stage(source, ctx, sink) ? sink.WrittenSpan.ToArray() : source;
    }
}
