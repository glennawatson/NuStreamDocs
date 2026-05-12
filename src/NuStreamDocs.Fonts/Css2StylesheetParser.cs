// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Parses a Google <c>css2</c> / Fontsource stylesheet into the individual <c>@font-face</c> entries (weight, style, <c>unicode-range</c>, woff2 URL).</summary>
public static class Css2StylesheetParser
{
    /// <summary>Default weight assumed when an <c>@font-face</c> omits <c>font-weight</c>.</summary>
    private const int DefaultWeight = 400;

    /// <summary>Radix for decimal digit accumulation.</summary>
    private const int DecimalRadix = 10;

    /// <summary>Byte length of the <c>url(</c> token.</summary>
    private const int UrlPrefixLength = 4;

    /// <summary>Byte length of a CSS comment delimiter (<c>/*</c> or <c>*/</c>).</summary>
    private const int CommentDelimiterLength = 2;

    /// <summary>Parses every <c>@font-face</c> rule in <paramref name="css"/>.</summary>
    /// <param name="css">UTF-8 stylesheet text.</param>
    /// <returns>One entry per rule that has a usable <c>src</c> URL.</returns>
    public static Css2FontFace[] Parse(ReadOnlySpan<byte> css)
    {
        List<Css2FontFace> faces = [];
        var pos = 0;
        while (pos < css.Length)
        {
            var faceStart = IndexOfFontFace(css, pos);
            if (faceStart < 0)
            {
                break;
            }

            var openRel = css[faceStart..].IndexOf((byte)'{');
            if (openRel < 0)
            {
                break;
            }

            var blockStart = faceStart + openRel + 1;
            var closeRel = css[blockStart..].IndexOf((byte)'}');
            if (closeRel < 0)
            {
                break;
            }

            var block = css.Slice(blockStart, closeRel);
            var url = ParseSrcUrl(block);
            if (url is { Length: > 0 })
            {
                ApiCompatString woff2Url = Encoding.UTF8.GetString(url);
                faces.Add(new(ParseWeight(block), ParseStyle(block), ParseUnicodeRange(block).ToArray(), LabelBefore(css, faceStart).ToArray(), woff2Url));
            }

            pos = blockStart + closeRel + 1;
        }

        return [.. faces];
    }

    /// <summary>Finds the next case-insensitive <c>@font-face</c> token at or after <paramref name="from"/>.</summary>
    /// <param name="css">Stylesheet bytes.</param>
    /// <param name="from">Start offset.</param>
    /// <returns>The offset of the <c>@</c>, or -1.</returns>
    private static int IndexOfFontFace(ReadOnlySpan<byte> css, int from)
    {
        ReadOnlySpan<byte> token = "@font-face"u8;
        for (var i = from; i + token.Length <= css.Length; i++)
        {
            if (css[i] == (byte)'@' && AsciiByteHelpers.StartsWithIgnoreAsciiCase(css, i, token))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns the text of the <c>/* ... */</c> comment immediately before <paramref name="faceStart"/> (css2 labels each block with its subset); empty when none.</summary>
    /// <param name="css">Stylesheet bytes.</param>
    /// <param name="faceStart">Offset of the <c>@font-face</c> token.</param>
    /// <returns>The trimmed comment text, or empty.</returns>
    private static ReadOnlySpan<byte> LabelBefore(ReadOnlySpan<byte> css, int faceStart)
    {
        var head = css[..faceStart];
        var closeIdx = head.LastIndexOf("*/"u8);
        if (closeIdx < 0)
        {
            return [];
        }

        var between = head[(closeIdx + CommentDelimiterLength)..];
        for (var i = 0; i < between.Length; i++)
        {
            if (!AsciiByteHelpers.IsAsciiWhitespace(between[i]))
            {
                return [];
            }
        }

        var openIdx = head[..closeIdx].LastIndexOf("/*"u8);
        return openIdx < 0 ? [] : AsciiByteHelpers.TrimAsciiWhitespace(head[(openIdx + CommentDelimiterLength)..closeIdx]);
    }

    /// <summary>Reads the numeric <c>font-weight</c> from an <c>@font-face</c> block; the first integer when a range is given.</summary>
    /// <param name="block">The block's inner bytes.</param>
    /// <returns>The weight, or 400 when absent.</returns>
    private static int ParseWeight(ReadOnlySpan<byte> block)
    {
        var value = FindPropertyValue(block, "font-weight"u8);
        var weight = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if (b is >= (byte)'0' and <= (byte)'9')
            {
                weight = (weight * DecimalRadix) + (b - (byte)'0');
            }
            else if (weight > 0)
            {
                break;
            }
        }

        return weight > 0 ? weight : DefaultWeight;
    }

    /// <summary>Reads <c>font-style</c> from an <c>@font-face</c> block.</summary>
    /// <param name="block">The block's inner bytes.</param>
    /// <returns><see cref="FontStyle.Italic"/> when the value contains <c>italic</c>, otherwise <see cref="FontStyle.Normal"/>.</returns>
    private static FontStyle ParseStyle(ReadOnlySpan<byte> block)
    {
        var value = FindPropertyValue(block, "font-style"u8);
        return value.IndexOf("italic"u8) >= 0 ? FontStyle.Italic : FontStyle.Normal;
    }

    /// <summary>Reads the trimmed <c>unicode-range</c> value from an <c>@font-face</c> block.</summary>
    /// <param name="block">The block's inner bytes.</param>
    /// <returns>The value bytes (empty when absent).</returns>
    private static ReadOnlySpan<byte> ParseUnicodeRange(ReadOnlySpan<byte> block) =>
        AsciiByteHelpers.TrimAsciiWhitespace(FindPropertyValue(block, "unicode-range"u8));

    /// <summary>Reads the first <c>url(...)</c> from the <c>src</c> declaration, stripping surrounding quotes.</summary>
    /// <param name="block">The block's inner bytes.</param>
    /// <returns>The URL bytes (empty when absent).</returns>
    private static ReadOnlySpan<byte> ParseSrcUrl(ReadOnlySpan<byte> block)
    {
        var src = FindPropertyValue(block, "src"u8);
        var open = src.IndexOf("url("u8);
        if (open < 0)
        {
            return [];
        }

        var rest = src[(open + UrlPrefixLength)..];
        var close = rest.IndexOf((byte)')');
        if (close < 0)
        {
            return [];
        }

        return AsciiByteHelpers.TrimAsciiWhitespace(rest[..close]).Trim((byte)'"').Trim((byte)'\'');
    }

    /// <summary>Finds the value of the CSS property <paramref name="name"/> within <paramref name="block"/> (the bytes between its <c>:</c> and the next <c>;</c>).</summary>
    /// <param name="block">The block's inner bytes.</param>
    /// <param name="name">Property name (lowercase UTF-8).</param>
    /// <returns>The value bytes (empty when the property is absent).</returns>
    private static ReadOnlySpan<byte> FindPropertyValue(ReadOnlySpan<byte> block, ReadOnlySpan<byte> name)
    {
        for (var i = 0; i + name.Length < block.Length; i++)
        {
            if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(block, i, name))
            {
                continue;
            }

            var after = block[(i + name.Length)..];
            var colon = after.IndexOf((byte)':');
            var semi = after.IndexOf((byte)';');
            if (colon < 0 || (semi >= 0 && semi < colon))
            {
                continue;
            }

            var value = after[(colon + 1)..];
            var end = value.IndexOf((byte)';');
            return end < 0 ? value : value[..end];
        }

        return [];
    }

    /// <summary>One parsed <c>@font-face</c> rule.</summary>
    /// <param name="Weight">Numeric font weight.</param>
    /// <param name="Style">Upright or italic.</param>
    /// <param name="UnicodeRange">UTF-8 <c>unicode-range</c> value (empty when absent).</param>
    /// <param name="SubsetName">UTF-8 subset name from the preceding <c>/* ... */</c> comment (e.g. <c>latin</c>); empty when the stylesheet doesn't label blocks.</param>
    /// <param name="Woff2Url">URL of the woff2 file referenced by <c>src</c>.</param>
    public readonly record struct Css2FontFace(int Weight, FontStyle Style, byte[] UnicodeRange, byte[] SubsetName, ApiCompatString Woff2Url);
}
