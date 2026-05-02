// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Stateless UTF-8 icon-shortcode rewriter for Material-classic and Material 3.
/// Recognizes <c>:material-{name}:</c> and
/// <c>:fontawesome-{style}-{name}:</c> shortcodes and emits the
/// span / <c>i</c>-tag shapes the bundled icon-font stylesheets
/// expect. Fenced and inline code pass through verbatim.
/// </summary>
public static class IconShortcodeRewriter
{
    /// <summary>Length of the <c>material-</c> prefix.</summary>
    private const int MaterialPrefixLength = 9;

    /// <summary>Length of the <c>fontawesome-</c> prefix.</summary>
    private const int FontAwesomePrefixLength = 12;

    /// <summary>Bytes that interrupt the bulk-copy fast path — backtick (inline code), colon (shortcode), newline (potential fence start). Cached so the IndexOfAny is vectorized.</summary>
    private static readonly SearchValues<byte> InterestingBytes = SearchValues.Create("`:\n"u8);

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>, emitting <paramref name="materialIconClass"/> for Material shortcodes.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="materialIconClass">UTF-8 class name to use for <c>:material-…:</c> shortcodes (e.g. <c>material-icons</c> or <c>material-symbols-outlined</c>).</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, ReadOnlySpan<byte> materialIconClass) =>
        Rewrite(source, writer, materialIconClass, resolver: null);

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>, consulting <paramref name="resolver"/> before falling back to the font-ligature span.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="materialIconClass">UTF-8 class name for <c>:material-…:</c> shortcodes that fall through to the ligature path.</param>
    /// <param name="resolver">Optional inline-SVG resolver; when supplied and a hit is returned, the SVG body is inlined verbatim instead of emitting a span.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, ReadOnlySpan<byte> materialIconClass, IIconResolver? resolver)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var i = 0;
        while (i < source.Length)
        {
            // Check fence-at-line-start first — fences begin on bytes (' ' / '`' / '~') we'd otherwise
            // scan past, and they can land just past a newline we just bulk-copied through.
            if (MarkdownCodeScanner.AtLineStart(source, i) && MarkdownCodeScanner.TryConsumeFence(source, i, out var fenceEnd))
            {
                writer.Write(source[i..fenceEnd]);
                i = fenceEnd;
                continue;
            }

            // Bulk-copy every byte up to the next interesting one. Avoids the per-byte GetSpan(1)
            // pump that previously dominated the rewriter's per-page cost on prose-heavy pages.
            var rel = source[i..].IndexOfAny(InterestingBytes);
            if (rel < 0)
            {
                writer.Write(source[i..]);
                return;
            }

            if (rel > 0)
            {
                writer.Write(source.Slice(i, rel));
                i += rel;
            }

            if (source[i] is (byte)'`')
            {
                var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, i);
                writer.Write(source[i..inlineEnd]);
                i = inlineEnd;
                continue;
            }

            if (source[i] is (byte)':' && TryRewriteShortcode(source, i, writer, materialIconClass, resolver, out var consumed))
            {
                i += consumed;
                continue;
            }

            // The interesting byte (newline, or a colon that didn't match a shortcode) wasn't actionable
            // — copy it through and resume the bulk scan after it.
            var dst = writer.GetSpan(1);
            dst[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Tries to match a recognized icon shortcode at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor on the leading <c>:</c>.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="materialIconClass">Class to emit for Material shortcodes.</param>
    /// <param name="resolver">Optional inline-SVG resolver consulted before the font-ligature fallback.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a shortcode was rewritten.</returns>
    private static bool TryRewriteShortcode(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, ReadOnlySpan<byte> materialIconClass, IIconResolver? resolver, out int consumed)
    {
        consumed = 0;
        var bodyStart = offset + 1;
        if (bodyStart >= source.Length)
        {
            return false;
        }

        var bodyEnd = ShortcodeScanner.ScanBody(source, bodyStart);
        if (bodyEnd == bodyStart || bodyEnd >= source.Length || source[bodyEnd] is not (byte)':')
        {
            return false;
        }

        var body = source[bodyStart..bodyEnd];
        if (!TryEmitMaterial(body, writer, materialIconClass, resolver) && !TryEmitFontAwesome(body, writer))
        {
            return false;
        }

        consumed = bodyEnd + 1 - offset;
        return true;
    }

    /// <summary>Emits the Material-icon markup for <paramref name="body"/> when it has the <c>material-</c> prefix.</summary>
    /// <param name="body">Shortcode body (between the colons).</param>
    /// <param name="writer">Sink.</param>
    /// <param name="materialIconClass">Class to emit (varies between Material classic and Material3).</param>
    /// <param name="resolver">Optional inline-SVG resolver consulted before the font-ligature fallback.</param>
    /// <returns>True when matched.</returns>
    private static bool TryEmitMaterial(ReadOnlySpan<byte> body, IBufferWriter<byte> writer, ReadOnlySpan<byte> materialIconClass, IIconResolver? resolver)
    {
        if (body.Length <= MaterialPrefixLength || !body.StartsWith("material-"u8))
        {
            return false;
        }

        var name = body[MaterialPrefixLength..];
        if (resolver?.TryResolve(name, writer) == true)
        {
            return true;
        }

        writer.Write("<span class=\""u8);
        writer.Write(materialIconClass);
        writer.Write("\">"u8);
        WriteWithUnderscores(writer, name);
        writer.Write("</span>"u8);
        return true;
    }

    /// <summary>Emits the FontAwesome <c>i</c>-tag for <paramref name="body"/> when it has the <c>fontawesome-{style}-</c> shape.</summary>
    /// <param name="body">Shortcode body.</param>
    /// <param name="writer">Sink.</param>
    /// <returns>True when matched.</returns>
    private static bool TryEmitFontAwesome(ReadOnlySpan<byte> body, IBufferWriter<byte> writer)
    {
        if (body.Length <= FontAwesomePrefixLength || !body.StartsWith("fontawesome-"u8))
        {
            return false;
        }

        var rest = body[FontAwesomePrefixLength..];
        var styleEnd = rest.IndexOf((byte)'-');
        if (styleEnd <= 0 || styleEnd == rest.Length - 1)
        {
            return false;
        }

        var style = rest[..styleEnd];
        if (!IsRecognizedFontAwesomeStyle(style))
        {
            return false;
        }

        var name = rest[(styleEnd + 1)..];
        writer.Write("<i class=\"fa-"u8);
        writer.Write(style);
        writer.Write(" fa-"u8);
        writer.Write(name);
        writer.Write("\"></i>"u8);
        return true;
    }

    /// <summary>Returns true when <paramref name="style"/> is one of the recognized FontAwesome family names.</summary>
    /// <param name="style">Bytes between <c>fontawesome-</c> and the icon name.</param>
    /// <returns>True for a recognized style.</returns>
    private static bool IsRecognizedFontAwesomeStyle(ReadOnlySpan<byte> style) =>
        style.SequenceEqual("solid"u8)
        || style.SequenceEqual("regular"u8)
        || style.SequenceEqual("brands"u8)
        || style.SequenceEqual("light"u8)
        || style.SequenceEqual("thin"u8)
        || style.SequenceEqual("duotone"u8);

    /// <summary>Writes <paramref name="name"/>, replacing <c>-</c> with <c>_</c> so Material Icons font ligatures resolve.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="name">Icon name (after <c>material-</c>).</param>
    private static void WriteWithUnderscores(IBufferWriter<byte> writer, ReadOnlySpan<byte> name)
    {
        var dst = writer.GetSpan(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            dst[i] = name[i] is (byte)'-' ? (byte)'_' : name[i];
        }

        writer.Advance(name.Length);
    }
}
