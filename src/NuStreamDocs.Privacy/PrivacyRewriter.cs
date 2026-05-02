// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Common;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy;

/// <summary>One-shot HTML rewriter that runs every privacy byte scanner against the same UTF-8 buffer.</summary>
/// <remarks>
/// Two non-URL stages (mixed-content upgrade, anchor hardening) and one combined URL stage rotate through two pooled
/// <see cref="ArrayBufferWriter{T}"/>s rented from <see cref="PageBuilderPool"/> — the same two-buffer shape
/// <c>AttrListRewriter</c> uses, so the rewrite is allocation-free across stages and the writers are returned to
/// the per-thread pool on dispose. The URL stage still walks the HTML in a single pass via
/// <see cref="ExternalUrlScanner.RewriteInto"/>, replacing the three sequential per-attribute-type stages the
/// previous shape paid for.
/// </remarks>
internal static class PrivacyRewriter
{
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

        using var rentalA = PageBuilderPool.Rent(html.Length);
        using var rentalB = PageBuilderPool.Rent(html.Length);
        var slots = new StageBuffers(rentalA.Writer, rentalB.Writer);

        ReadOnlyMemory<byte> current = default;
        var changed = false;

        if (options.UpgradeMixedContent)
        {
            TryRunMixedContent(html, ref current, ref slots, ref changed);
        }

        if (options.AddRelNoOpener || options.AddTargetBlank)
        {
            TryRunAnchor(html, options, ref current, ref slots, ref changed);
        }

        var ctx = new UrlRewriteContext(filter, registry);
        TryRunUrlPass(html, ctx, ref current, ref slots, ref changed);

        if (!changed)
        {
            return false;
        }

        sink.Write(current.Span);
        return true;
    }

    /// <summary>Runs the mixed-content upgrade stage and rotates the buffers when it rewrites.</summary>
    /// <param name="originalHtml">Original UTF-8 source (used as the input on the first stage that runs).</param>
    /// <param name="current">Current pipeline output; updated when this stage rewrites.</param>
    /// <param name="slots">Two-buffer rotation slots.</param>
    /// <param name="changed">Tracks whether any stage has rewritten so far.</param>
    private static void TryRunMixedContent(ReadOnlySpan<byte> originalHtml, ref ReadOnlyMemory<byte> current, ref StageBuffers slots, ref bool changed)
    {
        slots.Spare.ResetWrittenCount();
        var source = changed ? current.Span : originalHtml;
        if (!MixedContentBytes.RewriteInto(source, slots.Spare))
        {
            return;
        }

        current = slots.Spare.WrittenMemory;
        slots = slots.Swap();
        changed = true;
    }

    /// <summary>Runs the anchor-hardening stage and rotates the buffers when it rewrites.</summary>
    /// <param name="originalHtml">Original UTF-8 source.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="current">Current pipeline output; updated when this stage rewrites.</param>
    /// <param name="slots">Two-buffer rotation slots.</param>
    /// <param name="changed">Tracks whether any stage has rewritten so far.</param>
    private static void TryRunAnchor(ReadOnlySpan<byte> originalHtml, in PrivacyOptions options, ref ReadOnlyMemory<byte> current, ref StageBuffers slots, ref bool changed)
    {
        slots.Spare.ResetWrittenCount();
        var source = changed ? current.Span : originalHtml;
        if (!AnchorBytes.RewriteInto(source, options.AddRelNoOpener, options.AddTargetBlank, slots.Spare))
        {
            return;
        }

        current = slots.Spare.WrittenMemory;
        slots = slots.Swap();
        changed = true;
    }

    /// <summary>Runs the combined URL pass (asset attributes + srcset + inline-style url() in one walk) and rotates the buffers when it rewrites.</summary>
    /// <param name="originalHtml">Original UTF-8 source.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="current">Current pipeline output; updated when this stage rewrites.</param>
    /// <param name="slots">Two-buffer rotation slots.</param>
    /// <param name="changed">Tracks whether any stage has rewritten so far.</param>
    private static void TryRunUrlPass(ReadOnlySpan<byte> originalHtml, in UrlRewriteContext ctx, ref ReadOnlyMemory<byte> current, ref StageBuffers slots, ref bool changed)
    {
        slots.Spare.ResetWrittenCount();
        var source = changed ? current.Span : originalHtml;
        if (!ExternalUrlScanner.RewriteInto(source, ctx, slots.Spare))
        {
            return;
        }

        current = slots.Spare.WrittenMemory;
        slots = slots.Swap();
        changed = true;
    }

    /// <summary>Two-buffer rotation pair — <see cref="Spare"/> is the next stage's target; <see cref="Other"/> holds the prior output (or is empty before any stage has rewritten).</summary>
    /// <param name="Spare">Buffer the next stage writes into.</param>
    /// <param name="Other">Buffer holding the prior stage's output.</param>
    private readonly record struct StageBuffers(ArrayBufferWriter<byte> Spare, ArrayBufferWriter<byte> Other)
    {
        /// <summary>Swaps the two buffers; called after a stage commits to a rewrite so the next stage writes into the freed buffer.</summary>
        /// <returns>The swapped pair.</returns>
        [SuppressMessage(
            "SonarAnalyzer",
            "S2234:Parameters should be passed in the correct order",
            Justification = "Swap intentionally reverses the pair — the prior Other becomes the new Spare and vice versa.")]
        public StageBuffers Swap() => new(Other, Spare);
    }
}
