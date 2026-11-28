// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Social;

/// <summary>Extracts social title, description, image override, and SVG card slug from UTF-8 Markdown source.</summary>
internal static class SocialMetadataExtractor
{
    /// <summary>Length of the <c>---</c> YAML frontmatter delimiter and <c>.md</c> suffix.</summary>
    private const int ThreeByteMarkerLength = 3;

    /// <summary>Minimum quote pair length around a scalar YAML value.</summary>
    private const int QuotePairLength = 2;

    /// <summary>Maximum UTF-8 byte length kept for auto-extracted descriptions.</summary>
    private const int MaxDescriptionBytes = 160;

    /// <summary>UTF-8 continuation-byte mask.</summary>
    private const byte Utf8ContinuationMask = 0xC0;

    /// <summary>UTF-8 continuation-byte prefix value.</summary>
    private const byte Utf8ContinuationBits = 0x80;

    /// <summary>ASCII uppercase-to-lowercase offset.</summary>
    private const int AsciiLowerOffset = 'a' - 'A';

    /// <summary>Leading bytes that mark list items, blockquotes, tables, HTML blocks, or images rather than prose.</summary>
    private static readonly SearchValues<byte> NonProseLeadBytes = SearchValues.Create("-*+>|<!"u8);

    /// <summary>Extracts social metadata for <paramref name="relativePath"/> from <paramref name="source"/>.</summary>
    /// <param name="relativePath">Page path relative to input root.</param>
    /// <param name="source">UTF-8 Markdown source bytes.</param>
    /// <param name="siteName">Configured UTF-8 site name fallback for root index pages.</param>
    /// <returns>Resolved <see cref="SocialPageMetadata"/>.</returns>
    internal static SocialPageMetadata Extract(
        in FilePath relativePath,
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> siteName)
    {
        var body = ParseFrontMatter(source, out var title, out var description, out var image);
        ScanMarkdownBody(body, ref title, ref description);

        var resolvedTitle = title.Length > 0
            ? title
            : FallbackTitle(relativePath, siteName);
        var resolvedDescription = TruncateDescription(description);
        var slug = BuildSlug(relativePath);
        return new(resolvedTitle, resolvedDescription, image, slug);
    }

    /// <summary>Strips inline emphasis/code punctuation and truncates at a clean UTF-8 boundary.</summary>
    /// <param name="raw">Raw description bytes.</param>
    /// <returns>Cleaned UTF-8 description array.</returns>
    internal static byte[] TruncateDescription(ReadOnlySpan<byte> raw)
    {
        if (raw.IsEmpty)
        {
            return [];
        }

        var buffer = new byte[raw.Length];
        var written = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            var current = raw[i];
            if (current is (byte)'`' or (byte)'*')
            {
                continue;
            }

            buffer[written] = current;
            written++;
        }

        var cleaned = buffer.AsSpan(0, written).Trim(" \t"u8);
        if (cleaned.Length <= MaxDescriptionBytes)
        {
            return cleaned.ToArray();
        }

        var limit = MaxDescriptionBytes;
        while (limit > 0 && (cleaned[limit] & Utf8ContinuationMask) == Utf8ContinuationBits)
        {
            limit--;
        }

        var truncated = cleaned[..limit].TrimEnd(" \t"u8);
        return [.. truncated, .. "..."u8];
    }

    /// <summary>Builds a deterministic lowercase ASCII slug from <paramref name="relativePath"/>.</summary>
    /// <param name="relativePath">Page path relative to input root.</param>
    /// <returns>Non-empty ASCII slug suitable for an SVG filename.</returns>
    internal static string BuildSlug(in FilePath relativePath)
    {
        var span = relativePath.AsSpan();
        if (span.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            span = span[..^ThreeByteMarkerLength];
        }

        var chars = new char[span.Length];
        var length = 0;
        var previousHyphen = true;
        for (var i = 0; i < span.Length; i++)
        {
            AppendSlugChar(span[i], chars, ref length, ref previousHyphen);
        }

        if (length > 0 && chars[length - 1] == '-')
        {
            length--;
        }

        return length == 0 ? "index" : new string(chars, 0, length);
    }

    /// <summary>Appends a normalized slug character or collapsed hyphen separator into <paramref name="chars"/>.</summary>
    /// <param name="ch">Source character.</param>
    /// <param name="chars">Destination character buffer.</param>
    /// <param name="length">Current output length.</param>
    /// <param name="previousHyphen">Tracks whether the preceding emitted character was a hyphen.</param>
    private static void AppendSlugChar(char ch, char[] chars, ref int length, ref bool previousHyphen)
    {
        if (char.IsAsciiLetterOrDigit(ch))
        {
            chars[length] = ch is >= 'A' and <= 'Z' ? (char)(ch + AsciiLowerOffset) : ch;
            length++;
            previousHyphen = false;
            return;
        }

        if (previousHyphen)
        {
            return;
        }

        chars[length] = '-';
        length++;
        previousHyphen = true;
    }

    /// <summary>Parses top-level <c>title</c>, <c>description</c>, and <c>image</c>/<c>social_image</c> keys from YAML frontmatter and returns the remaining body span.</summary>
    /// <param name="source">Full UTF-8 Markdown source.</param>
    /// <param name="title">Extracted title bytes.</param>
    /// <param name="description">Extracted description bytes.</param>
    /// <param name="image">Extracted image override bytes.</param>
    /// <returns>Body span after frontmatter (or <paramref name="source"/> when no valid frontmatter block exists).</returns>
    private static ReadOnlySpan<byte> ParseFrontMatter(
        ReadOnlySpan<byte> source,
        out byte[] title,
        out byte[] description,
        out byte[] image)
    {
        title = [];
        description = [];
        image = [];

        if (!TrySliceFrontMatter(source, out var frontMatter, out var body))
        {
            return source;
        }

        var remaining = frontMatter;
        while (!remaining.IsEmpty)
        {
            var line = ReadLine(ref remaining);
            ApplyFrontMatterLine(line, ref title, ref description, ref image);
        }

        return body;
    }

    /// <summary>Attempts to split <paramref name="source"/> into a YAML frontmatter block and Markdown body.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="frontMatter">Frontmatter inner lines.</param>
    /// <param name="body">Markdown body following the closing delimiter.</param>
    /// <returns><c>true</c> when a closed frontmatter header was found; otherwise <c>false</c>.</returns>
    private static bool TrySliceFrontMatter(
        ReadOnlySpan<byte> source,
        out ReadOnlySpan<byte> frontMatter,
        out ReadOnlySpan<byte> body)
    {
        frontMatter = default;
        body = default;
        var rest = source;
        var firstLine = ReadLine(ref rest);
        if (!firstLine.SequenceEqual("---"u8))
        {
            return false;
        }

        var cursor = rest;
        while (!cursor.IsEmpty)
        {
            var lineStartLength = cursor.Length;
            var line = ReadLine(ref cursor);
            if (!line.SequenceEqual("---"u8))
            {
                continue;
            }

            frontMatter = rest[..(rest.Length - lineStartLength)];
            body = cursor;
            return true;
        }

        return false;
    }

    /// <summary>Matches a single YAML frontmatter line against supported social keys.</summary>
    /// <param name="line">Single YAML line without line endings.</param>
    /// <param name="title">Updated when the key is <c>title</c>.</param>
    /// <param name="description">Updated when the key is <c>description</c>.</param>
    /// <param name="image">Updated when the key is <c>image</c> or <c>social_image</c>.</param>
    private static void ApplyFrontMatterLine(
        ReadOnlySpan<byte> line,
        ref byte[] title,
        ref byte[] description,
        ref byte[] image)
    {
        if (!TrySplitYamlKeyValue(line, out var key, out var value))
        {
            return;
        }

        if (key.SequenceEqual("title"u8))
        {
            title = value.ToArray();
        }
        else if (key.SequenceEqual("description"u8))
        {
            description = value.ToArray();
        }
        else if (key.SequenceEqual("social_image"u8) || (key.SequenceEqual("image"u8) && image.Length == 0))
        {
            image = value.ToArray();
        }
    }

    /// <summary>Parses a top-level <c>key: value</c> YAML line into unquoted spans.</summary>
    /// <param name="line">Raw line bytes.</param>
    /// <param name="key">Trimmed key span.</param>
    /// <param name="value">Trimmed and unquoted scalar value span.</param>
    /// <returns><c>true</c> when a non-empty top-level scalar pair was found.</returns>
    private static bool TrySplitYamlKeyValue(
        ReadOnlySpan<byte> line,
        out ReadOnlySpan<byte> key,
        out ReadOnlySpan<byte> value)
    {
        key = default;
        value = default;
        if (line.IsEmpty || line[0] is (byte)' ' or (byte)'\t' or (byte)'#')
        {
            return false;
        }

        var colon = line.IndexOf((byte)':');
        if (colon <= 0)
        {
            return false;
        }

        key = line[..colon].Trim(" \t"u8);
        value = Unquote(line[(colon + 1)..].Trim(" \t"u8));
        return !value.IsEmpty;
    }

    /// <summary>Strips matching single or double quotes around a scalar value.</summary>
    /// <param name="value">Trimmed value bytes.</param>
    /// <returns>Unquoted span.</returns>
    private static ReadOnlySpan<byte> Unquote(ReadOnlySpan<byte> value) =>
        value.Length >= QuotePairLength
        && ((value[0] == (byte)'"' && value[^1] == (byte)'"')
            || (value[0] == (byte)'\'' && value[^1] == (byte)'\''))
            ? value[1..^1]
            : value;

    /// <summary>Scans Markdown body lines for the first ATX heading and first plain prose paragraph line.</summary>
    /// <param name="markdownBody">Markdown body span.</param>
    /// <param name="headingTitle">Populated with heading bytes when empty.</param>
    /// <param name="summaryText">Populated with prose bytes when empty.</param>
    private static void ScanMarkdownBody(
        ReadOnlySpan<byte> markdownBody,
        ref byte[] headingTitle,
        ref byte[] summaryText)
    {
        var remaining = markdownBody;
        var inFence = false;
        while (!remaining.IsEmpty && (headingTitle.Length == 0 || summaryText.Length == 0))
        {
            var trimmed = ReadLine(ref remaining).Trim(" \t"u8);
            if (trimmed.IsEmpty)
            {
                continue;
            }

            if (IsFenceDelimiter(trimmed))
            {
                inFence = !inFence;
                continue;
            }

            if (!inFence)
            {
                ConsumeBodyLine(trimmed, ref headingTitle, ref summaryText);
            }
        }
    }

    /// <summary>Records a non-fenced Markdown line as a heading or prose summary when still needed.</summary>
    /// <param name="trimmed">Non-empty trimmed line.</param>
    /// <param name="headingTitle">Target heading bytes.</param>
    /// <param name="summaryText">Target prose bytes.</param>
    private static void ConsumeBodyLine(
        ReadOnlySpan<byte> trimmed,
        ref byte[] headingTitle,
        ref byte[] summaryText)
    {
        if (trimmed[0] == (byte)'#')
        {
            if (headingTitle.Length == 0)
            {
                headingTitle = ExtractHeadingText(trimmed).ToArray();
            }

            return;
        }

        if (summaryText.Length == 0 && IsProseLine(trimmed))
        {
            summaryText = trimmed.ToArray();
        }
    }

    /// <summary>Checks whether <paramref name="trimmed"/> starts with a fenced code delimiter (<c>```</c> or <c>~~~</c>).</summary>
    /// <param name="trimmed">Trimmed line bytes.</param>
    /// <returns><c>true</c> when the line opens or closes a code fence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFenceDelimiter(ReadOnlySpan<byte> trimmed) =>
        trimmed.Length >= ThreeByteMarkerLength
        && (trimmed.StartsWith("```"u8) || trimmed.StartsWith("~~~"u8));

    /// <summary>Strips leading and trailing <c>#</c> markers from an ATX heading line.</summary>
    /// <param name="line">Heading line starting with <c>#</c>.</param>
    /// <returns>Trimmed heading text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> ExtractHeadingText(ReadOnlySpan<byte> line) =>
        line.TrimStart((byte)'#').TrimEnd((byte)'#').Trim(" \t"u8);

    /// <summary>Determines whether <paramref name="trimmed"/> starts like prose rather than list, table, blockquote, HTML, or image syntax.</summary>
    /// <param name="trimmed">Non-empty trimmed line.</param>
    /// <returns><c>true</c> when suitable as a summary sentence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsProseLine(ReadOnlySpan<byte> trimmed) =>
        !NonProseLeadBytes.Contains(trimmed[0]);

    /// <summary>Computes a fallback title from <paramref name="relativePath"/> or <paramref name="siteName"/>.</summary>
    /// <param name="relativePath">Page relative path.</param>
    /// <param name="siteName">Site name bytes.</param>
    /// <returns>UTF-8 title bytes.</returns>
    private static byte[] FallbackTitle(in FilePath relativePath, ReadOnlySpan<byte> siteName)
    {
        var stem = relativePath.FileNameWithoutExtension;
        if (string.IsNullOrEmpty(stem) || stem.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            return !siteName.IsEmpty ? siteName.ToArray() : [.. "Home"u8];
        }

        return Utf8PathHumanizer.HumanizePathName(stem.AsSpan());
    }

    /// <summary>Reads one line from <paramref name="remaining"/>, stripping <c>\r</c> and <c>\n</c>.</summary>
    /// <param name="remaining">Remaining span, advanced past the newline.</param>
    /// <returns>Single line without line terminators.</returns>
    private static ReadOnlySpan<byte> ReadLine(ref ReadOnlySpan<byte> remaining)
    {
        var newline = remaining.IndexOf((byte)'\n');
        ReadOnlySpan<byte> line;
        if (newline < 0)
        {
            line = remaining;
            remaining = default;
        }
        else
        {
            line = remaining[..newline];
            remaining = remaining[(newline + 1)..];
        }

        return !line.IsEmpty && line[^1] == (byte)'\r' ? line[..^1] : line;
    }
}
