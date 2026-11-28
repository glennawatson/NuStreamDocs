// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Export;

/// <summary>Extracts chapter content, plain-text title, and deterministic section IDs from rendered HTML pages.</summary>
internal static class ExportHtmlExtractor
{
    /// <summary>Prefix prepended to every generated chapter anchor ID.</summary>
    private const string SectionPrefix = "nd-page-";

    /// <summary>Length of the <c>.md</c> file extension.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>ASCII uppercase-to-lowercase offset.</summary>
    private const int AsciiLowerOffset = 'a' - 'A';

    /// <summary>Gets the UTF-8 pilcrow sequence (<c>U+00B6</c>) appended by permalink renderers.</summary>
    private static ReadOnlySpan<byte> Utf8Pilcrow => [0xC2, 0xB6];

    /// <summary>Extracts an <see cref="ExportPageEntry"/> from a rendered page.</summary>
    /// <param name="relativePath">Relative Markdown source path.</param>
    /// <param name="servedUrl">Served URL bytes.</param>
    /// <param name="html">Rendered page HTML bytes.</param>
    /// <returns>Harvested page entry.</returns>
    internal static ExportPageEntry Extract(string relativePath, ReadOnlySpan<byte> servedUrl, ReadOnlySpan<byte> html)
    {
        var normalizedKey = relativePath.Replace('\\', '/');
        var sectionId = BuildSectionId(normalizedKey);
        var bodySlice = ExtractElementInner(html, "<article"u8, "</article>"u8);
        if (bodySlice.IsEmpty)
        {
            bodySlice = ExtractElementInner(html, "<body"u8, "</body>"u8);
        }

        if (bodySlice.IsEmpty)
        {
            bodySlice = html;
        }

        var title = ExtractHeadingText(bodySlice);
        if (title.Length == 0)
        {
            title = ExtractHeadingText(ExtractElementInner(html, "<title"u8, "</title>"u8));
        }

        if (title.Length == 0)
        {
            title = Encoding.UTF8.GetBytes(normalizedKey);
        }

        return new(normalizedKey, servedUrl.ToArray(), sectionId, title, bodySlice.Trim(" \t\r\n"u8).ToArray());
    }

    /// <summary>Builds a lowercase ASCII anchor ID (<c>nd-page-...</c>) from <paramref name="relativePath"/>.</summary>
    /// <param name="relativePath">Forward-slashed relative path.</param>
    /// <returns>UTF-8 anchor ID bytes.</returns>
    internal static byte[] BuildSectionId(string relativePath)
    {
        var stem = relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? relativePath[..^MarkdownExtensionLength]
            : relativePath;

        StringBuilder builder = new(SectionPrefix.Length + stem.Length);
        _ = builder.Append(SectionPrefix);
        var prevDash = true;
        for (var i = 0; i < stem.Length; i++)
        {
            var ch = stem[i];
            if (char.IsAsciiLetterOrDigit(ch))
            {
                _ = builder.Append(ch is >= 'A' and <= 'Z' ? (char)(ch + AsciiLowerOffset) : ch);
                prevDash = false;
            }
            else if (!prevDash)
            {
                _ = builder.Append('-');
                prevDash = true;
            }
        }

        if (builder.Length > SectionPrefix.Length && builder[^1] == '-')
        {
            builder.Length--;
        }

        if (builder.Length == SectionPrefix.Length)
        {
            _ = builder.Append("root");
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>Extracts the inner slice between an opening tag prefix (such as <c>&lt;article</c>) and <paramref name="closeTag"/>.</summary>
    /// <param name="html">Source HTML bytes.</param>
    /// <param name="openPrefix">Opening tag prefix without <c>&gt;</c>.</param>
    /// <param name="closeTag">Closing tag bytes.</param>
    /// <returns>Inner HTML slice, or empty when absent.</returns>
    private static ReadOnlySpan<byte> ExtractElementInner(
        ReadOnlySpan<byte> html,
        ReadOnlySpan<byte> openPrefix,
        ReadOnlySpan<byte> closeTag)
    {
        var openIdx = html.IndexOf(openPrefix);
        if (openIdx < 0)
        {
            return default;
        }

        var afterPrefix = openIdx + openPrefix.Length;
        if (afterPrefix < html.Length && AsciiByteHelpers.IsAsciiLetter(html[afterPrefix]))
        {
            return default;
        }

        var closeAngleOffset = html[afterPrefix..].IndexOf((byte)'>');
        if (closeAngleOffset < 0)
        {
            return default;
        }

        var innerStart = afterPrefix + closeAngleOffset + 1;
        var closeRel = html[innerStart..].LastIndexOf(closeTag);
        return closeRel < 0 ? default : html.Slice(innerStart, closeRel);
    }

    /// <summary>Extracts stripped text from the first <c>&lt;h1&gt;</c> element (or directly from <paramref name="fragment"/> when plain text).</summary>
    /// <param name="fragment">HTML fragment.</param>
    /// <returns>Trimmed UTF-8 plain text.</returns>
    private static byte[] ExtractHeadingText(ReadOnlySpan<byte> fragment)
    {
        if (fragment.IsEmpty)
        {
            return [];
        }

        var h1Inner = ExtractElementInner(fragment, "<h1"u8, "</h1>"u8);
        var source = ResolveTextSource(fragment, h1Inner);
        if (source.IsEmpty)
        {
            return [];
        }

        ArrayBufferWriter<byte> plain = new(source.Length);
        var inTag = false;
        for (var i = 0; i < source.Length; i++)
        {
            var b = source[i];
            if (b == (byte)'<')
            {
                inTag = true;
            }
            else if (b == (byte)'>')
            {
                inTag = false;
            }
            else if (!inTag)
            {
                var dst = plain.GetSpan(1);
                dst[0] = b;
                plain.Advance(1);
            }
        }

        var cleaned = plain.WrittenSpan.Trim(" \t\r\n"u8);
        if (cleaned.EndsWith(Utf8Pilcrow))
        {
            cleaned = cleaned[..^Utf8Pilcrow.Length].TrimEnd(" \t"u8);
        }

        return cleaned.ToArray();
    }

    /// <summary>Chooses <paramref name="h1Inner"/> when present, otherwise plain untagged <paramref name="fragment"/>.</summary>
    /// <param name="fragment">Candidate fragment.</param>
    /// <param name="h1Inner">Extracted <c>&lt;h1&gt;</c> inner slice.</param>
    /// <returns>Selected slice.</returns>
    private static ReadOnlySpan<byte> ResolveTextSource(ReadOnlySpan<byte> fragment, ReadOnlySpan<byte> h1Inner)
    {
        if (!h1Inner.IsEmpty)
        {
            return h1Inner;
        }

        return fragment.IndexOf((byte)'<') < 0 ? fragment : default;
    }
}
