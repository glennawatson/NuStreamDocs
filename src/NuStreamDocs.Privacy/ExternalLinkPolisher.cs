// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Stateless HTML rewriter that hardens external <c>&lt;a&gt;</c>
/// anchors with privacy-preserving attributes (<c>rel="noopener noreferrer"</c>,
/// optional <c>target="_blank"</c>) and optionally upgrades mixed
/// <c>http://</c> URLs to <c>https://</c> on both anchors and asset
/// attributes.
/// </summary>
internal static class ExternalLinkPolisher
{
    /// <summary>Returns true when <paramref name="html"/> may contain anchors or http URLs we'd touch.</summary>
    /// <param name="html">Page HTML.</param>
    /// <returns>True when the cheap pre-filter matches.</returns>
    public static bool MayHaveExternalLinks(ReadOnlySpan<byte> html) =>
        html.IndexOf("href"u8) >= 0 || html.IndexOf("http://"u8) >= 0;

    /// <summary>Polishes <paramref name="html"/> per <paramref name="options"/>.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="options">Plugin options; only the link/mixed-content fields are read.</param>
    /// <returns>Rewritten HTML, or the original bytes when nothing changed.</returns>
    public static byte[] Polish(ReadOnlySpan<byte> html, in PrivacyOptions options)
    {
        if (!options.AddRelNoOpener && !options.AddTargetBlank && !options.UpgradeMixedContent)
        {
            return [.. html];
        }

        using var rental1 = PageBuilderPool.Rent(html.Length);
        var sink = rental1.Writer;
        var changedMixed = options.UpgradeMixedContent && MixedContentBytes.RewriteInto(html, sink);
        var stage1 = changedMixed ? sink.WrittenSpan : html;

        if (!options.AddRelNoOpener && !options.AddTargetBlank)
        {
            return stage1.ToArray();
        }

        using var rental2 = PageBuilderPool.Rent(stage1.Length);
        var sink2 = rental2.Writer;
        var changedAnchors = AnchorBytes.RewriteInto(stage1, options.AddRelNoOpener, options.AddTargetBlank, sink2);
        return changedAnchors ? sink2.WrittenSpan.ToArray() : stage1.ToArray();
    }

    /// <summary>Rewrites every non-loopback <c>http://</c> URL in <c>src</c>/<c>href</c> attributes of <paramref name="html"/> to <c>https://</c>.</summary>
    /// <param name="html">Page HTML as a string.</param>
    /// <returns>Rewritten HTML; the same instance when no URL needed upgrading.</returns>
    public static string UpgradeMixedContent(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var bytes = Encoding.UTF8.GetBytes(html);
        using var rental = PageBuilderPool.Rent(bytes.Length);
        var sink = rental.Writer;
        return MixedContentBytes.RewriteInto(bytes, sink) ? Encoding.UTF8.GetString(sink.WrittenSpan) : html;
    }

    /// <summary>Adds <c>rel="noopener noreferrer"</c> and/or <c>target="_blank"</c> to every external anchor in <paramref name="html"/>.</summary>
    /// <param name="html">Page HTML as a string.</param>
    /// <param name="options">Plugin options; only <see cref="PrivacyOptions.AddRelNoOpener"/> / <see cref="PrivacyOptions.AddTargetBlank"/> are read.</param>
    /// <returns>Rewritten HTML; the same instance when no anchor matched.</returns>
    public static string HardenAnchors(string html, in PrivacyOptions options)
    {
        ArgumentNullException.ThrowIfNull(html);
        var bytes = Encoding.UTF8.GetBytes(html);
        using var rental = PageBuilderPool.Rent(bytes.Length);
        var sink = rental.Writer;
        var changed = AnchorBytes.RewriteInto(bytes, options.AddRelNoOpener, options.AddTargetBlank, sink);
        return changed ? Encoding.UTF8.GetString(sink.WrittenSpan) : html;
    }
}
