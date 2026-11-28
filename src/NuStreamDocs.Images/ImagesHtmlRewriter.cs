// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.Images;

/// <summary>Rewrites <c>&lt;img&gt;</c> tags in rendered HTML with intrinsic dimensions, lazy/eager loading, async decoding, and optional <c>&lt;figure&gt;</c> captions.</summary>
internal static class ImagesHtmlRewriter
{
    /// <summary>Length of a <c>/&gt;</c> self-closing suffix.</summary>
    private const int SelfCloseSuffixLength = 2;

    /// <summary>Offset where attributes start after <c>&lt;img</c>.</summary>
    private const int ImgTagNameLength = 4;

    /// <summary>Scratch buffer size for integer formatting.</summary>
    private const int IntBufferLength = 16;

    /// <summary>ASCII whitespace bytes separating HTML attributes.</summary>
    private static readonly SearchValues<byte> HtmlWhitespace = SearchValues.Create(" \t\r\n"u8);

    /// <summary>Gets the opening <c>&lt;img </c> prefix bytes.</summary>
    private static ReadOnlySpan<byte> ImgTagOpen => "<img "u8;

    /// <summary>Gets the opening <c>&lt;figure</c> prefix bytes.</summary>
    private static ReadOnlySpan<byte> FigureOpen => "<figure"u8;

    /// <summary>Gets the closing <c>&lt;/figure&gt;</c> tag bytes.</summary>
    private static ReadOnlySpan<byte> FigureClose => "</figure>"u8;

    /// <summary>Rewrites all <c>&lt;img &gt;</c> elements in <paramref name="html"/> into <paramref name="output"/>.</summary>
    /// <param name="html">Input UTF-8 HTML.</param>
    /// <param name="options">Active plugin options.</param>
    /// <param name="dimensionResolver">Callback resolving local <c>src</c> UTF-8 bytes to <see cref="ImageDimensions"/>.</param>
    /// <param name="output">Destination buffer writer.</param>
    internal static void Rewrite(
        ReadOnlySpan<byte> html,
        in ImagesOptions options,
        Func<byte[], ImageDimensions?> dimensionResolver,
        IBufferWriter<byte> output)
    {
        var cursor = 0;
        var imageIndex = 0;
        var figureDepth = 0;

        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf(ImgTagOpen);
            if (rel < 0)
            {
                output.Write(html[cursor..]);
                return;
            }

            var imgStart = cursor + rel;
            var skipped = html[cursor..imgStart];
            UpdateFigureDepth(skipped, ref figureDepth);
            output.Write(skipped);

            var tagEndRel = FindTagClose(html[imgStart..]);
            if (tagEndRel < 0)
            {
                output.Write(html[imgStart..]);
                return;
            }

            var tagSlice = html.Slice(imgStart, tagEndRel + 1);
            RewriteSingleImg(tagSlice, imageIndex, figureDepth > 0, options, dimensionResolver, output);
            imageIndex++;
            cursor = imgStart + tagEndRel + 1;
        }
    }

    /// <summary>Updates <paramref name="figureDepth"/> across an HTML segment preceding the next <c>&lt;img&gt;</c>.</summary>
    /// <param name="segment">HTML slice.</param>
    /// <param name="figureDepth">Running <c>&lt;figure&gt;</c> nesting counter.</param>
    private static void UpdateFigureDepth(ReadOnlySpan<byte> segment, ref int figureDepth)
    {
        var pos = 0;
        while (pos < segment.Length)
        {
            var lt = segment[pos..].IndexOf((byte)'<');
            if (lt < 0)
            {
                return;
            }

            var at = pos + lt;
            var rest = segment[at..];
            if (rest.StartsWith(FigureOpen)
                && (rest.Length == FigureOpen.Length || rest[FigureOpen.Length] is (byte)'>' or (byte)' '))
            {
                figureDepth++;
            }
            else if (rest.StartsWith(FigureClose) && figureDepth > 0)
            {
                figureDepth--;
            }

            pos = at + 1;
        }
    }

    /// <summary>Finds the closing <c>&gt;</c> of an HTML tag while skipping quoted attribute values.</summary>
    /// <param name="fromTagStart">HTML starting at <c>&lt;img </c>.</param>
    /// <returns>Relative offset of <c>&gt;</c>, or <c>-1</c> if unclosed.</returns>
    private static int FindTagClose(ReadOnlySpan<byte> fromTagStart)
    {
        byte activeQuote = 0;
        for (var i = 0; i < fromTagStart.Length; i++)
        {
            var current = fromTagStart[i];
            if (activeQuote != 0)
            {
                if (current == activeQuote)
                {
                    activeQuote = 0;
                }

                continue;
            }

            if (current is (byte)'"' or (byte)'\'')
            {
                activeQuote = current;
            }
            else if (current == (byte)'>')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Emits a single rewritten <c>&lt;img&gt;</c> tag and optional <c>&lt;figure&gt;</c> wrapper.</summary>
    /// <param name="tag">Complete <c>&lt;img ...&gt;</c> slice.</param>
    /// <param name="imageIndex">Zero-based image index on the current page.</param>
    /// <param name="insideFigure">Whether the tag is already inside an ancestor <c>&lt;figure&gt;</c>.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="dimensionResolver">Dimension lookup callback.</param>
    /// <param name="output">Destination buffer.</param>
    private static void RewriteSingleImg(
        ReadOnlySpan<byte> tag,
        int imageIndex,
        bool insideFigure,
        in ImagesOptions options,
        Func<byte[], ImageDimensions?> dimensionResolver,
        IBufferWriter<byte> output)
    {
        var selfClosing = tag.EndsWith("/>"u8);
        var beforeClose = selfClosing ? tag[..^SelfCloseSuffixLength].TrimEnd((byte)' ') : tag[..^1];
        var parsed = ScanImgAttributes(beforeClose[ImgTagNameLength..]);

        var wrapFigure = options.WrapTitledInFigure && parsed.Title.Length > 0 && !insideFigure;
        if (wrapFigure)
        {
            output.Write("<figure class=\"nd-figure\">"u8);
        }

        output.Write(beforeClose);
        EmitMissingDimensions(parsed, options.InjectDimensions, dimensionResolver, output);
        EmitLoadingAndDecoding(parsed, imageIndex, options, output);
        output.Write(selfClosing ? " />"u8 : ">"u8);

        if (!wrapFigure)
        {
            return;
        }

        output.Write("<figcaption class=\"nd-figcaption\">"u8);
        output.Write(parsed.Title);
        output.Write("</figcaption></figure>"u8);
    }

    /// <summary>Appends <c>width</c> and <c>height</c> when missing and resolvable from <paramref name="dimensionResolver"/>.</summary>
    /// <param name="parsed">Scanned image attributes.</param>
    /// <param name="injectDimensions">Whether dimension lookup is enabled.</param>
    /// <param name="dimensionResolver">Dimension lookup callback.</param>
    /// <param name="output">Destination buffer.</param>
    private static void EmitMissingDimensions(
        in ImgAttributeSummary parsed,
        bool injectDimensions,
        Func<byte[], ImageDimensions?> dimensionResolver,
        IBufferWriter<byte> output)
    {
        if (!injectDimensions || parsed.HasWidth || parsed.HasHeight || IsRemoteOrDataUri(parsed.Src))
        {
            return;
        }

        if (dimensionResolver(parsed.Src) is not ImageDimensions dims)
        {
            return;
        }

        output.Write(" width=\""u8);
        WriteInt(output, dims.Width);
        output.Write("\" height=\""u8);
        WriteInt(output, dims.Height);
        output.Write("\""u8);
    }

    /// <summary>Appends <c>loading</c> and <c>decoding</c> attributes when enabled and not already declared.</summary>
    /// <param name="parsed">Scanned image attributes.</param>
    /// <param name="imageIndex">Zero-based image index on the page.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="output">Destination buffer.</param>
    private static void EmitLoadingAndDecoding(
        in ImgAttributeSummary parsed,
        int imageIndex,
        in ImagesOptions options,
        IBufferWriter<byte> output)
    {
        if (!parsed.HasLoading)
        {
            if (imageIndex < options.EagerFirstCount)
            {
                output.Write(" loading=\"eager\""u8);
            }
            else if (options.LazyLoading)
            {
                output.Write(" loading=\"lazy\""u8);
            }
        }

        if (options.AsyncDecoding && !parsed.HasDecoding)
        {
            output.Write(" decoding=\"async\""u8);
        }
    }

    /// <summary>Walks all attributes inside an <c>&lt;img&gt;</c> tag once in a quote-aware pass.</summary>
    /// <param name="attrs">Attribute slice after <c>&lt;img</c>.</param>
    /// <returns>Collected attribute summary.</returns>
    private static ImgAttributeSummary ScanImgAttributes(ReadOnlySpan<byte> attrs)
    {
        var summary = new ImgAttributeSummary([], [], false, false, false, false);
        var cursor = 0;
        while (TryReadNextAttribute(attrs, ref cursor, out var name, out var value))
        {
            summary = ApplyAttribute(name, value, summary);
        }

        return summary;
    }

    /// <summary>Records one parsed attribute into <paramref name="summary"/>.</summary>
    /// <param name="name">Attribute name bytes.</param>
    /// <param name="value">Attribute value bytes.</param>
    /// <param name="summary">Current summary.</param>
    /// <returns>Updated summary.</returns>
    private static ImgAttributeSummary ApplyAttribute(
        ReadOnlySpan<byte> name,
        ReadOnlySpan<byte> value,
        in ImgAttributeSummary summary)
    {
        if (name.SequenceEqual("src"u8))
        {
            return summary with { Src = value.ToArray() };
        }

        if (name.SequenceEqual("title"u8))
        {
            return summary with { Title = value.ToArray() };
        }

        if (name.SequenceEqual("width"u8))
        {
            return summary with { HasWidth = true };
        }

        if (name.SequenceEqual("height"u8))
        {
            return summary with { HasHeight = true };
        }

        if (name.SequenceEqual("loading"u8))
        {
            return summary with { HasLoading = true };
        }

        return name.SequenceEqual("decoding"u8)
            ? summary with { HasDecoding = true }
            : summary;
    }

    /// <summary>Reads the next HTML attribute name and optional value from <paramref name="attrs"/>.</summary>
    /// <param name="attrs">Tag attribute slice.</param>
    /// <param name="cursor">Current byte offset.</param>
    /// <param name="name">Attribute name slice.</param>
    /// <param name="value">Attribute value slice (empty for boolean attributes).</param>
    /// <returns><c>true</c> when an attribute was read.</returns>
    private static bool TryReadNextAttribute(
        ReadOnlySpan<byte> attrs,
        ref int cursor,
        out ReadOnlySpan<byte> name,
        out ReadOnlySpan<byte> value)
    {
        while (cursor < attrs.Length && HtmlWhitespace.Contains(attrs[cursor]))
        {
            cursor++;
        }

        if (cursor >= attrs.Length)
        {
            name = default;
            value = default;
            return false;
        }

        var nameStart = cursor;
        while (cursor < attrs.Length && attrs[cursor] != (byte)'=' && !HtmlWhitespace.Contains(attrs[cursor]))
        {
            cursor++;
        }

        name = attrs[nameStart..cursor];
        if (cursor >= attrs.Length || attrs[cursor] != (byte)'=')
        {
            value = default;
            return true;
        }

        cursor++;
        value = ReadAttrValue(attrs, ref cursor);
        return true;
    }

    /// <summary>Reads a quoted or unquoted attribute value starting right past <c>=</c>.</summary>
    /// <param name="attrs">Attribute buffer.</param>
    /// <param name="cursor">Offset immediately after <c>=</c>.</param>
    /// <returns>Attribute value slice.</returns>
    private static ReadOnlySpan<byte> ReadAttrValue(ReadOnlySpan<byte> attrs, ref int cursor)
    {
        if (cursor >= attrs.Length)
        {
            return default;
        }

        var first = attrs[cursor];
        if (first is (byte)'"' or (byte)'\'')
        {
            cursor++;
            var start = cursor;
            var closeRel = attrs[start..].IndexOf(first);
            cursor = start + closeRel + 1;
            return attrs.Slice(start, closeRel);
        }

        var bareStart = cursor;
        while (cursor < attrs.Length && !HtmlWhitespace.Contains(attrs[cursor]))
        {
            cursor++;
        }

        return attrs[bareStart..cursor];
    }

    /// <summary>Checks whether <paramref name="src"/> is an external URL or inline data URI.</summary>
    /// <param name="src">UTF-8 <c>src</c> attribute value.</param>
    /// <returns><c>true</c> when non-local.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRemoteOrDataUri(ReadOnlySpan<byte> src) =>
        src.IsEmpty
        || src.StartsWith("https://"u8)
        || src.StartsWith("http://"u8)
        || src.StartsWith("//"u8)
        || src.StartsWith("data:"u8);

    /// <summary>Formats an integer as ASCII digits into <paramref name="output"/>.</summary>
    /// <param name="output">Destination buffer.</param>
    /// <param name="value">Integer value.</param>
    private static void WriteInt(IBufferWriter<byte> output, int value)
    {
        Span<byte> scratch = stackalloc byte[IntBufferLength];
        _ = value.TryFormat(scratch, out var written, provider: CultureInfo.InvariantCulture);
        output.Write(scratch[..written]);
    }

    /// <summary>Immutable summary of attributes declared on a single <c>&lt;img&gt;</c> tag.</summary>
    /// <param name="Src">UTF-8 <c>src</c> attribute bytes.</param>
    /// <param name="Title">UTF-8 <c>title</c> attribute bytes.</param>
    /// <param name="HasWidth">Whether <c>width</c> is present.</param>
    /// <param name="HasHeight">Whether <c>height</c> is present.</param>
    /// <param name="HasLoading">Whether <c>loading</c> is present.</param>
    /// <param name="HasDecoding">Whether <c>decoding</c> is present.</param>
    private readonly record struct ImgAttributeSummary(
        byte[] Src,
        byte[] Title,
        bool HasWidth,
        bool HasHeight,
        bool HasLoading,
        bool HasDecoding);
}
