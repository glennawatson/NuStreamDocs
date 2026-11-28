// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Social;

/// <summary>Renders deterministic <c>1200x630</c> branded SVG social preview cards in UTF-8.</summary>
internal static class SocialSvgGenerator
{
    /// <summary>Left margin X coordinate for text elements.</summary>
    private const int TextLeftX = 108;

    /// <summary>Baseline Y coordinate for the site name label.</summary>
    private const int SiteNameBaselineY = 128;

    /// <summary>First baseline Y coordinate for the title block.</summary>
    private const int TitleFirstBaselineY = 250;

    /// <summary>Line height between wrapped title lines.</summary>
    private const int TitleLineHeight = 68;

    /// <summary>Maximum byte width per title line before wrapping.</summary>
    private const int TitleWrapBytes = 32;

    /// <summary>First baseline Y coordinate for the description block.</summary>
    private const int DescriptionFirstBaselineY = 416;

    /// <summary>Line height between wrapped description lines.</summary>
    private const int DescriptionLineHeight = 42;

    /// <summary>Maximum byte width per description line before wrapping.</summary>
    private const int DescriptionWrapBytes = 64;

    /// <summary>Baseline Y coordinate for the footer URL.</summary>
    private const int FooterBaselineY = 560;

    /// <summary>Initial buffer capacity for an SVG card.</summary>
    private const int InitialSvgCapacity = 2048;

    /// <summary>UTF-8 continuation-byte mask.</summary>
    private const byte Utf8ContinuationMask = 0xC0;

    /// <summary>UTF-8 continuation-byte prefix value.</summary>
    private const byte Utf8ContinuationBits = 0x80;

    /// <summary>Renders a complete <c>1200x630</c> SVG card for a page.</summary>
    /// <param name="title">UTF-8 page title.</param>
    /// <param name="description">UTF-8 page summary.</param>
    /// <param name="siteName">UTF-8 site name header.</param>
    /// <param name="siteUrl">UTF-8 site URL footer.</param>
    /// <param name="options">Card color options.</param>
    /// <returns>Complete UTF-8 SVG document bytes.</returns>
    internal static byte[] RenderCard(
        ReadOnlySpan<byte> title,
        ReadOnlySpan<byte> description,
        ReadOnlySpan<byte> siteName,
        ReadOnlySpan<byte> siteUrl,
        in SocialOptions options)
    {
        var writer = new ArrayBufferWriter<byte>(InitialSvgCapacity);
        var background = !options.BackgroundColor.AsSpan().IsEmpty ? options.BackgroundColor.AsSpan() : "#0f172a"u8;
        var accent = !options.AccentColor.AsSpan().IsEmpty ? options.AccentColor.AsSpan() : "#2563eb"u8;

        writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"630\" viewBox=\"0 0 1200 630\" role=\"img\" aria-label=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, title, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\">\n<rect width=\"1200\" height=\"630\" fill=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, background, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\"/>\n<rect width=\"1200\" height=\"14\" fill=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, accent, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\"/>\n<rect x=\"72\" y=\"84\" width=\"8\" height=\"462\" rx=\"4\" fill=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, accent, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\"/>\n"u8);

        if (!siteName.IsEmpty)
        {
            var siteStyle = new TextStyle("28"u8, "600"u8, "#93c5fd"u8);
            WriteTextElement(writer, SiteNameBaselineY, siteStyle, siteName);
        }

        var titleStyle = new TextStyle("54"u8, "700"u8, "#f8fafc"u8);
        WriteWrappedBlock(writer, title, TitleWrapBytes, TitleFirstBaselineY, TitleLineHeight, titleStyle);

        if (!description.IsEmpty)
        {
            var descStyle = new TextStyle("28"u8, "400"u8, "#cbd5e1"u8);
            WriteWrappedBlock(writer, description, DescriptionWrapBytes, DescriptionFirstBaselineY, DescriptionLineHeight, descStyle);
        }

        if (!siteUrl.IsEmpty)
        {
            var urlStyle = new TextStyle("22"u8, "500"u8, "#94a3b8"u8);
            WriteTextElement(writer, FooterBaselineY, urlStyle, TrimTrailingSlash(siteUrl));
        }

        writer.Write("</svg>\n"u8);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Splits <paramref name="text"/> into up to two space-broken lines.</summary>
    /// <param name="text">UTF-8 text.</param>
    /// <param name="maxBytesPerLine">Maximum bytes per line.</param>
    /// <param name="first">First line slice.</param>
    /// <param name="second">Second line slice (empty when everything fits on line one).</param>
    /// <param name="truncated"><c>true</c> when text exceeded two lines.</param>
    internal static void SplitTwoLines(
        ReadOnlySpan<byte> text,
        int maxBytesPerLine,
        out ReadOnlySpan<byte> first,
        out ReadOnlySpan<byte> second,
        out bool truncated)
    {
        var trimmed = text.Trim(" \t"u8);
        if (trimmed.Length <= maxBytesPerLine)
        {
            first = trimmed;
            second = default;
            truncated = false;
            return;
        }

        first = TakeLine(trimmed, maxBytesPerLine, out var remainder);
        if (remainder.Length <= maxBytesPerLine)
        {
            second = remainder;
            truncated = false;
            return;
        }

        second = TakeLine(remainder, maxBytesPerLine, out _);
        truncated = true;
    }

    /// <summary>Splits <paramref name="text"/> into at most two word-wrapped lines and emits SVG <c>&lt;text&gt;</c> elements.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="text">UTF-8 text to wrap.</param>
    /// <param name="maxBytesPerLine">Soft byte budget per line.</param>
    /// <param name="firstY">Y coordinate of the first line.</param>
    /// <param name="lineHeight">Vertical step to the second line.</param>
    /// <param name="style">Font styling tokens.</param>
    private static void WriteWrappedBlock(
        IBufferWriter<byte> writer,
        ReadOnlySpan<byte> text,
        int maxBytesPerLine,
        int firstY,
        int lineHeight,
        in TextStyle style)
    {
        SplitTwoLines(text, maxBytesPerLine, out var firstLine, out var secondLine, out var truncated);
        WriteTextElement(writer, firstY, style, firstLine);
        if (secondLine.IsEmpty)
        {
            return;
        }

        var lineTwo = truncated ? [.. secondLine, .. "..."u8] : secondLine;
        WriteTextElement(writer, firstY + lineHeight, style, lineTwo);
    }

    /// <summary>Takes a single wrapped line at the last space within <paramref name="maxBytes"/> (or hard-cuts on a UTF-8 boundary when a single token exceeds the limit).</summary>
    /// <param name="input">Trimmed input text.</param>
    /// <param name="maxBytes">Maximum byte count.</param>
    /// <param name="remainder">Trimmed text after the cut.</param>
    /// <returns>Line slice.</returns>
    private static ReadOnlySpan<byte> TakeLine(
        ReadOnlySpan<byte> input,
        int maxBytes,
        out ReadOnlySpan<byte> remainder)
    {
        var window = input[..maxBytes];
        var lastSpace = window.LastIndexOf((byte)' ');
        if (lastSpace > 0)
        {
            remainder = input[(lastSpace + 1)..].TrimStart((byte)' ');
            return input[..lastSpace];
        }

        var cut = maxBytes;
        while (cut > 0 && (input[cut] & Utf8ContinuationMask) == Utf8ContinuationBits)
        {
            cut--;
        }

        remainder = input[cut..].TrimStart((byte)' ');
        return input[..cut];
    }

    /// <summary>Writes one styled SVG <c>&lt;text&gt;</c> element.</summary>
    /// <param name="writer">Destination sink.</param>
    /// <param name="baselineY">Baseline vertical coordinate.</param>
    /// <param name="style">Typography tokens.</param>
    /// <param name="content">Unescaped UTF-8 text content.</param>
    private static void WriteTextElement(
        IBufferWriter<byte> writer,
        int baselineY,
        in TextStyle style,
        ReadOnlySpan<byte> content)
    {
        writer.Write("<text x=\""u8);
        WriteIntAscii(writer, TextLeftX);
        writer.Write("\" y=\""u8);
        WriteIntAscii(writer, baselineY);
        writer.Write("\" font-family=\"system-ui,-apple-system,'Segoe UI',Roboto,sans-serif\" font-size=\""u8);
        writer.Write(style.FontSize);
        writer.Write("\" font-weight=\""u8);
        writer.Write(style.FontWeight);
        writer.Write("\" fill=\""u8);
        writer.Write(style.Fill);
        writer.Write("\">"u8);
        XmlEntityEscaper.WriteEscaped(writer, content, XmlEntityEscaper.Mode.Xml);
        writer.Write("</text>\n"u8);
    }

    /// <summary>Formats a non-negative integer as ASCII digits directly into <paramref name="writer"/>.</summary>
    /// <param name="writer">Destination buffer writer.</param>
    /// <param name="value">Integer to format.</param>
    private static void WriteIntAscii(IBufferWriter<byte> writer, int value)
    {
        const int MaxIntDigits = 16;
        Span<byte> scratch = stackalloc byte[MaxIntDigits];
        _ = value.TryFormat(scratch, out var written, provider: CultureInfo.InvariantCulture);
        writer.Write(scratch[..written]);
    }

    /// <summary>Strips a single trailing slash from <paramref name="url"/> for clean display in card footers.</summary>
    /// <param name="url">UTF-8 URL bytes.</param>
    /// <returns>Trimmed URL span.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> TrimTrailingSlash(ReadOnlySpan<byte> url) =>
        url[^1] == (byte)'/' ? url[..^1] : url;

    /// <summary>Bundles SVG typography attributes to keep helper parameter lists compact.</summary>
    /// <param name="fontSize">Font size attribute bytes.</param>
    /// <param name="fontWeight">Font weight attribute bytes.</param>
    /// <param name="fill">Text fill color bytes.</param>
    private readonly ref struct TextStyle(
        ReadOnlySpan<byte> fontSize,
        ReadOnlySpan<byte> fontWeight,
        ReadOnlySpan<byte> fill)
    {
        /// <summary>Gets the font-size attribute bytes.</summary>
        public ReadOnlySpan<byte> FontSize { get; } = fontSize;

        /// <summary>Gets the font-weight attribute bytes.</summary>
        public ReadOnlySpan<byte> FontWeight { get; } = fontWeight;

        /// <summary>Gets the fill attribute bytes.</summary>
        public ReadOnlySpan<byte> Fill { get; } = fill;
    }
}
