// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using NuStreamDocs.Common;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy;

/// <summary>Stateless HTML rewriter that finds <c>http(s)://</c> URLs in asset-bearing attributes and rewrites them to local paths obtained from an <see cref="ExternalAssetRegistry"/>.</summary>
/// <remarks>
/// One walker, one pass. The combined scanner unions the first-byte
/// sets of the three URL-rewrite shapes (asset attributes, srcset,
/// and inline-style <c>url()</c> blocks) and dispatches to the
/// appropriate matcher inline — eliminating the two intermediate
/// <see cref="ArrayBufferWriter{T}"/> + <c>ToArray</c> round-trips
/// the previous three-stage shape paid per page.
/// </remarks>
internal static class ExternalUrlScanner
{
    /// <summary>First-byte candidate set covering every URL-rewrite shape — <c>&lt;</c> for inline-style blocks, <c>s/S/h/H</c> for asset / srcset attributes.</summary>
    private static readonly SearchValues<byte> CombinedFirstBytes = SearchValues.Create("<sShH"u8);

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

    /// <summary>
    /// Walks <paramref name="html"/> once and rewrites every external
    /// URL match (asset attributes, srcset entries, and inline-style
    /// <c>url()</c> tokens) directly into <paramref name="sink"/>.
    /// </summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="ctx">URL-rewrite context (filter + registry).</param>
    /// <param name="sink">Destination sink; only written to when at least one URL is rewritten.</param>
    /// <returns>True when at least one URL was rewritten; false when the input passed through unchanged.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, in UrlRewriteContext ctx, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var changed = false;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOfAny(CombinedFirstBytes);
            if (rel < 0)
            {
                break;
            }

            var p = cursor + rel;
            if (TryDispatchAt(html, p, ctx, sink, ref lastEmit, out var advanceTo))
            {
                changed = true;
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

    /// <summary>Rewrites every external URL found in <c>src</c>/<c>href</c>/<c>srcset</c> attributes plus inline <c>&lt;style&gt;</c> bodies of <paramref name="html"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="registry">URL registry; entries created on demand.</param>
    /// <param name="filter">Per-URL filter encapsulating the allow/skip lists.</param>
    /// <returns>The rewritten HTML (or the input bytes verbatim when nothing matched).</returns>
    public static byte[] Rewrite(ReadOnlySpan<byte> html, ExternalAssetRegistry registry, HostFilter filter)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(filter);

        var ctx = new UrlRewriteContext(filter, registry);
        using var rental = PageBuilderPool.Rent(html.Length);
        var sink = rental.Writer;
        return RewriteInto(html, ctx, sink) ? sink.WrittenSpan.ToArray() : html.ToArray();
    }

    /// <summary>Dispatches the candidate at <paramref name="p"/> to the matcher for its first-byte class.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when a URL was rewritten.</returns>
    private static bool TryDispatchAt(ReadOnlySpan<byte> html, int p, in UrlRewriteContext ctx, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        var b = html[p];
        if (b is (byte)'<')
        {
            return InlineStyleBlockBytes.TryRewriteBlock(html, p, ctx, sink, ref lastEmit, out advanceTo);
        }

        // Try srcset first when the candidate could plausibly start one (longer name, more specific). Fall through to src / href on miss.
        if (b is (byte)'s' or (byte)'S'
            && SrcsetBytes.TryRewriteAt(html, p, ctx, sink, ref lastEmit, out advanceTo))
        {
            return true;
        }

        return AssetAttributeBytes.TryRewriteAt(html, p, ctx, sink, ref lastEmit, out advanceTo);
    }
}
