// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy;

/// <summary>
/// One-shot HTML rewriter that runs every privacy byte scanner against
/// the same UTF-8 buffer, threading the buffer through without copying
/// when a stage doesn't change anything.
/// </summary>
/// <remarks>
/// Each stage takes a UTF-8 span, writes into an
/// <see cref="ArrayBufferWriter{T}"/>, and either returns true (a fresh
/// buffer becomes the working source) or false (the prior buffer is
/// passed straight to the next stage). The page-level
/// <c>Encoding.UTF8.GetString</c> round-trip the previous version paid
/// is gone — every pass operates on bytes from start to finish.
/// </remarks>
internal static class PrivacyRewriter
{
    /// <summary>Delegate matching every byte-level URL stage.</summary>
    /// <param name="source">UTF-8 source span.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when the stage rewrote at least one URL.</returns>
    private delegate bool UrlStage(ReadOnlySpan<byte> source, in UrlRewriteContext ctx, IBufferWriter<byte> sink);

    /// <summary>Runs every privacy rewrite pass against <paramref name="html"/>, writing the result directly into <paramref name="sink"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="registry">URL registry.</param>
    /// <param name="filter">Host filter.</param>
    /// <param name="sink">Destination buffer; only written to when at least one pass changed the input.</param>
    /// <returns>True when bytes were written to <paramref name="sink"/>; false when no pass changed the input.</returns>
    public static bool TryRewriteInto(ReadOnlySpan<byte> html, in PrivacyOptions options, ExternalAssetRegistry registry, HostFilter filter, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(sink);

        // Materialise once so we can swap byte[] references between stages —
        // ReadOnlySpan<byte> can't escape stages (it's a ref struct).
        var current = html.ToArray();
        var changed = false;

        if (options.UpgradeMixedContent)
        {
            current = RunMixedContent(current, ref changed);
        }

        if (options.AddRelNoOpener || options.AddTargetBlank)
        {
            current = RunAnchor(current, options, ref changed);
        }

        var ctx = new UrlRewriteContext(filter, registry);
        current = RunUrlStage(current, ctx, AssetAttributeBytes.RewriteInto, ref changed);
        current = RunUrlStage(current, ctx, SrcsetBytes.RewriteInto, ref changed);
        current = RunUrlStage(current, ctx, InlineStyleBlockBytes.RewriteInto, ref changed);

        if (!changed)
        {
            return false;
        }

        sink.Write(current);
        return true;
    }

    /// <summary>Runs the mixed-content upgrade stage.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="changed">Tracks whether any stage has rewritten so far.</param>
    /// <returns>The current buffer (fresh array when rewritten, the input array otherwise).</returns>
    private static byte[] RunMixedContent(byte[] source, ref bool changed)
    {
        var sink = new ArrayBufferWriter<byte>(source.Length);
        if (!MixedContentBytes.RewriteInto(source, sink))
        {
            return source;
        }

        changed = true;
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Runs the anchor-hardening stage.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="changed">Tracks whether any stage has rewritten so far.</param>
    /// <returns>The current buffer.</returns>
    private static byte[] RunAnchor(byte[] source, in PrivacyOptions options, ref bool changed)
    {
        var sink = new ArrayBufferWriter<byte>(source.Length);
        if (!AnchorBytes.RewriteInto(source, options.AddRelNoOpener, options.AddTargetBlank, sink))
        {
            return source;
        }

        changed = true;
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Runs one URL-rewrite stage.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="stage">Stage to run.</param>
    /// <param name="changed">Tracks whether any stage has rewritten so far.</param>
    /// <returns>The current buffer.</returns>
    private static byte[] RunUrlStage(byte[] source, in UrlRewriteContext ctx, UrlStage stage, ref bool changed)
    {
        var sink = new ArrayBufferWriter<byte>(source.Length);
        if (!stage(source, ctx, sink))
        {
            return source;
        }

        changed = true;
        return sink.WrittenSpan.ToArray();
    }
}
