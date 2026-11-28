// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Llms;

/// <summary>Pure UTF-8 scanners that extract titles, summaries, sections, and clean Markdown bodies for <see cref="LlmsPlugin"/>.</summary>
internal static class LlmsMarkdownExtractor
{
    /// <summary>Bit mask identifying UTF-8 continuation bytes (<c>10xxxxxx</c>).</summary>
    private const byte Utf8ContinuationMask = 0xC0;

    /// <summary>Tag value of a UTF-8 continuation byte (<c>0x80</c>).</summary>
    private const byte Utf8ContinuationTag = 0x80;

    /// <summary>Minimum fence run length (<c>```</c> or <c>~~~</c>).</summary>
    private const int MinFenceLength = 3;

    /// <summary>ASCII case offset between lowercase and uppercase letters.</summary>
    private const byte AsciiCaseOffset = 32;

    /// <summary>Builds an <see cref="LlmsPageEntry"/> when the page should be included in LLM indexes.</summary>
    /// <param name="relativePath">Source-relative page path.</param>
    /// <param name="source">Raw UTF-8 Markdown source (front matter + body).</param>
    /// <param name="useDirectoryUrls">Whether directory URLs are active.</param>
    /// <param name="options">Configured LLM output options.</param>
    /// <param name="entry">Populated entry when included.</param>
    /// <returns><c>true</c> when the page is eligible for inclusion; otherwise, <c>false</c>.</returns>
    internal static bool TryCreateEntry(
        in FilePath relativePath,
        ReadOnlySpan<byte> source,
        bool useDirectoryUrls,
        in LlmsOptions options,
        out LlmsPageEntry entry)
    {
        entry = default;
        if (relativePath.IsEmpty || IsOptedOut(source))
        {
            return false;
        }

        var urlPath = Utf8MarkdownUrl.FromRelativePath(relativePath, useDirectoryUrls);
        var body = StripFrontmatter(source);
        var title = ExtractTitle(source, body, relativePath);
        var summary = ExtractSummary(source, body, options.MaxSummaryBytes);
        var section = ExtractSection(relativePath, options.DefaultSectionTitle);

        entry = new(
            section,
            urlPath,
            relativePath.Replace('\\', '/'),
            title,
            summary,
            YamlByteScanner.TrimWhitespace(body).ToArray());
        return true;
    }

    /// <summary>Returns <c>true</c> when front matter sets <c>llms: false</c>.</summary>
    /// <param name="source">UTF-8 Markdown source.</param>
    /// <returns><c>true</c> when opted out.</returns>
    internal static bool IsOptedOut(ReadOnlySpan<byte> source)
    {
        var flag = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "llms"u8));
        return !flag.IsEmpty && AsciiByteHelpers.EqualsIgnoreAsciiCase(flag, "false"u8);
    }

    /// <summary>Slices past leading YAML front matter when present.</summary>
    /// <param name="source">UTF-8 Markdown source.</param>
    /// <returns>Body slice without front matter.</returns>
    internal static ReadOnlySpan<byte> StripFrontmatter(ReadOnlySpan<byte> source) =>
        YamlByteScanner.TryFindFrontmatter(source, out _, out var bodyStart)
            ? source[bodyStart..]
            : source;

    /// <summary>Extracts the page title from front matter, the first ATX heading, or the file stem.</summary>
    /// <param name="source">Full UTF-8 Markdown source.</param>
    /// <param name="body">Front-matter-stripped Markdown body.</param>
    /// <param name="relativePath">Page path fallback.</param>
    /// <returns>UTF-8 title bytes.</returns>
    internal static byte[] ExtractTitle(ReadOnlySpan<byte> source, ReadOnlySpan<byte> body, in FilePath relativePath)
    {
        var frontTitle = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "title"u8));
        if (frontTitle.IsEmpty)
        {
            frontTitle = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "Title"u8));
        }

        if (!frontTitle.IsEmpty)
        {
            return frontTitle.ToArray();
        }

        var heading = FindFirstHeading(body);
        return !heading.IsEmpty ? heading.ToArray() : HumanizeStem(relativePath);
    }

    /// <summary>Extracts a concise summary from front matter (<c>description</c>/<c>summary</c>) or the first prose line.</summary>
    /// <param name="source">Full UTF-8 Markdown source.</param>
    /// <param name="body">Front-matter-stripped Markdown body.</param>
    /// <param name="maxBytes">Maximum UTF-8 byte length.</param>
    /// <returns>UTF-8 summary bytes (empty when none exists).</returns>
    internal static byte[] ExtractSummary(ReadOnlySpan<byte> source, ReadOnlySpan<byte> body, int maxBytes)
    {
        var desc = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "description"u8));
        if (desc.IsEmpty)
        {
            desc = YamlByteScanner.Unquote(FrontmatterValueExtractor.GetScalar(source, "summary"u8));
        }

        var candidate = !desc.IsEmpty ? desc : FindFirstProseLine(body);
        return candidate.IsEmpty ? [] : TruncateUtf8(candidate, maxBytes);
    }

    /// <summary>Extracts the humanized top-level directory name or returns <paramref name="defaultSection"/> for root files.</summary>
    /// <param name="relativePath">Source-relative file path.</param>
    /// <param name="defaultSection">Fallback section heading for root-level files.</param>
    /// <returns>UTF-8 section heading bytes.</returns>
    internal static byte[] ExtractSection(in FilePath relativePath, byte[] defaultSection)
    {
        var span = relativePath.AsSpan();
        var sep = span.IndexOfAny('/', '\\');
        if (sep <= 0)
        {
            return defaultSection.Length > 0 ? defaultSection : LlmsOptions.DefaultRootSectionTitle;
        }

        return HumanizeChars(span[..sep]);
    }

    /// <summary>Truncates <paramref name="text"/> to at most <paramref name="maxBytes"/> without splitting multi-byte UTF-8 sequences.</summary>
    /// <param name="text">UTF-8 summary text.</param>
    /// <param name="maxBytes">Positive byte budget.</param>
    /// <returns>UTF-8 bytes, appending <c>...</c> when truncated.</returns>
    internal static byte[] TruncateUtf8(ReadOnlySpan<byte> text, int maxBytes)
    {
        if (text.Length <= maxBytes)
        {
            return text.ToArray();
        }

        var cut = maxBytes;
        while (cut > 0 && (text[cut] & Utf8ContinuationMask) == Utf8ContinuationTag)
        {
            cut--;
        }

        var trimmed = YamlByteScanner.TrimWhitespace(text[..cut]);
        var ellipsis = "..."u8;
        var result = new byte[trimmed.Length + ellipsis.Length];
        trimmed.CopyTo(result);
        ellipsis.CopyTo(result.AsSpan(trimmed.Length));
        return result;
    }

    /// <summary>Finds the text of the first ATX heading (<c># ...</c>) outside fenced code blocks.</summary>
    /// <param name="body">UTF-8 Markdown body.</param>
    /// <returns>Trimmed heading text, or empty when no heading exists.</returns>
    internal static ReadOnlySpan<byte> FindFirstHeading(ReadOnlySpan<byte> body)
    {
        var inFence = false;
        var cursor = 0;
        while (cursor < body.Length)
        {
            var lineEnd = Utf8LineSpan.LfLineEnd(body, cursor);
            var line = YamlByteScanner.TrimWhitespace(body[cursor..lineEnd]);
            cursor = lineEnd;
            if (IsFenceDelimiter(line))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || !TryExtractAtxHeading(line, out var heading))
            {
                continue;
            }

            return heading;
        }

        return [];
    }

    /// <summary>Finds the first non-heading, non-code, non-list prose line in <paramref name="body"/>.</summary>
    /// <param name="body">UTF-8 Markdown body.</param>
    /// <returns>First prose line, or empty when none exists.</returns>
    internal static ReadOnlySpan<byte> FindFirstProseLine(ReadOnlySpan<byte> body)
    {
        var inFence = false;
        var cursor = 0;
        while (cursor < body.Length)
        {
            var lineEnd = Utf8LineSpan.LfLineEnd(body, cursor);
            var line = YamlByteScanner.TrimWhitespace(body[cursor..lineEnd]);
            cursor = lineEnd;
            if (IsFenceDelimiter(line))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || !IsProseStarter(line))
            {
                continue;
            }

            return line;
        }

        return [];
    }

    /// <summary>Extracts ATX heading text when <paramref name="line"/> begins with one or more <c>#</c> bytes followed by whitespace.</summary>
    /// <param name="line">Trimmed line bytes.</param>
    /// <param name="heading">Extracted heading text on success.</param>
    /// <returns><c>true</c> when <paramref name="line"/> is an ATX heading.</returns>
    private static bool TryExtractAtxHeading(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> heading)
    {
        heading = default;
        if (line.IsEmpty || line[0] is not (byte)'#')
        {
            return false;
        }

        var hashes = 0;
        while (hashes < line.Length && line[hashes] is (byte)'#')
        {
            hashes++;
        }

        if (hashes >= line.Length || line[hashes] is not ((byte)' ' or (byte)'\t'))
        {
            return false;
        }

        heading = YamlByteScanner.TrimWhitespace(YamlByteScanner.TrimWhitespace(line[hashes..]).TrimEnd((byte)'#'));
        return true;
    }

    /// <summary>Returns <c>true</c> when <paramref name="line"/> opens or closes a backtick or tilde code fence.</summary>
    /// <param name="line">Trimmed line bytes.</param>
    /// <returns><c>true</c> for <c>```</c> or <c>~~~</c> prefixes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFenceDelimiter(ReadOnlySpan<byte> line) =>
        line.Length >= MinFenceLength
        && (line.StartsWith("```"u8) || line.StartsWith("~~~"u8));

    /// <summary>Returns <c>true</c> when <paramref name="line"/> starts ordinary paragraph prose rather than block syntax.</summary>
    /// <param name="line">Trimmed line bytes.</param>
    /// <returns><c>true</c> when suitable as a summary sentence.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsProseStarter(ReadOnlySpan<byte> line) =>
        !line.IsEmpty
        && line[0] is not ((byte)'#' or (byte)'>' or (byte)'-' or (byte)'*' or (byte)'+' or (byte)'|' or (byte)'<' or (byte)'!');

    /// <summary>Converts a file stem into title-cased UTF-8 bytes.</summary>
    /// <param name="relativePath">Source-relative file path.</param>
    /// <returns>Humanized UTF-8 bytes.</returns>
    private static byte[] HumanizeStem(in FilePath relativePath)
    {
        var stem = relativePath.FileNameWithoutExtension.AsSpan();
        return stem.Equals("index", StringComparison.OrdinalIgnoreCase) && !relativePath.Directory.IsEmpty
            ? HumanizeChars(relativePath.Directory.Name.AsSpan())
            : HumanizeChars(stem);
    }

    /// <summary>Encodes <paramref name="chars"/> as UTF-8, replacing <c>-</c>/<c>_</c> with spaces and capitalizing the first ASCII letter.</summary>
    /// <param name="chars">Identifier characters.</param>
    /// <returns>Humanized UTF-8 bytes.</returns>
    private static byte[] HumanizeChars(ReadOnlySpan<char> chars)
    {
        var utf8 = Encoding.UTF8.GetBytes(chars.ToArray());
        for (var i = 0; i < utf8.Length; i++)
        {
            if (utf8[i] is (byte)'-' or (byte)'_')
            {
                utf8[i] = (byte)' ';
            }
        }

        if (utf8.Length > 0 && utf8[0] is >= (byte)'a' and <= (byte)'z')
        {
            utf8[0] = (byte)(utf8[0] - AsciiCaseOffset);
        }

        return utf8;
    }
}
