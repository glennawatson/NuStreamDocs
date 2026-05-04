// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Reads Wyam-style YAML frontmatter from a UTF-8 markdown source.
/// </summary>
/// <remarks>
/// Wyam used a simple <c>key: value</c> dialect — no nested objects,
/// no anchors. The parser is intentionally narrow: it pulls the keys
/// the blog plugin understands (<c>Title</c>, <c>Author</c>,
/// <c>Published</c>, <c>Tags</c>, <c>NoTitle</c>, <c>IsBlog</c>) and
/// ignores everything else. Tags accept either a single value or a
/// comma-separated list; whitespace around commas is trimmed.
/// </remarks>
public static class WyamFrontmatterReader
{
    /// <summary>Length of the frontmatter fence (<c>---</c>).</summary>
    private const int FrontmatterFenceLength = 3;

    /// <summary>Index of the third dash in the frontmatter fence.</summary>
    private const int FrontmatterFenceLastIndex = 2;

    /// <summary>Parses frontmatter from the head of <paramref name="markdown"/>.</summary>
    /// <param name="markdown">Full UTF-8 markdown source.</param>
    /// <returns>The parsed frontmatter values plus the byte offset where the body begins.</returns>
    public static FrontmatterResult Parse(ReadOnlySpan<byte> markdown)
    {
        if (!IsFrontmatterFence(markdown))
        {
            return new([], [], default, [], false, 0);
        }

        var firstLineEnd = markdown[FrontmatterFenceLength..].IndexOf((byte)'\n');
        if (firstLineEnd < 0)
        {
            return new([], [], default, [], false, 0);
        }

        firstLineEnd += FrontmatterFenceLength;
        var bodyStart = -1;
        var cursor = firstLineEnd + 1;
        var title = Array.Empty<byte>();
        var author = Array.Empty<byte>();
        var published = default(DateOnly);
        var tags = new List<byte[]>(4);
        var isBlog = false;

        while (cursor < markdown.Length)
        {
            var rest = markdown[cursor..];
            var lineEnd = rest.IndexOf((byte)'\n');
            if (lineEnd < 0)
            {
                lineEnd = rest.Length;
            }

            var line = rest[..lineEnd].TrimEnd((byte)'\r');
            if (IsFrontmatterFence(line))
            {
                bodyStart = cursor + lineEnd + 1;
                break;
            }

            ParseLine(line, ref title, ref author, ref published, tags, ref isBlog);
            cursor += lineEnd + 1;
        }

        if (bodyStart < 0)
        {
            return new([], [], default, [], false, 0);
        }

        return new(title, author, published, [.. tags], isBlog, bodyStart);
    }

    /// <summary>Applies one frontmatter line to the running parse state.</summary>
    /// <param name="line">Trimmed line.</param>
    /// <param name="title">Title accumulator.</param>
    /// <param name="author">Author accumulator.</param>
    /// <param name="published">Published-date accumulator.</param>
    /// <param name="tags">Tag accumulator.</param>
    /// <param name="isBlog"><c>IsBlog</c> accumulator.</param>
    private static void ParseLine(
        ReadOnlySpan<byte> line,
        ref byte[] title,
        ref byte[] author,
        ref DateOnly published,
        List<byte[]> tags,
        ref bool isBlog)
    {
        var sep = line.IndexOf((byte)':');
        if (sep <= 0)
        {
            return;
        }

        var key = AsciiByteHelpers.TrimAsciiWhitespace(line[..sep]);
        var value = AsciiByteHelpers.TrimAsciiWhitespace(line[(sep + 1)..]);

        if (AsciiByteHelpers.EqualsIgnoreAsciiCase(key, "title"u8))
        {
            title = value.ToArray();
        }
        else if (AsciiByteHelpers.EqualsIgnoreAsciiCase(key, "author"u8))
        {
            author = value.ToArray();
        }
        else if (AsciiByteHelpers.EqualsIgnoreAsciiCase(key, "published"u8))
        {
            if (TryParseDate(value, out var parsed))
            {
                published = parsed;
            }
        }
        else if (AsciiByteHelpers.EqualsIgnoreAsciiCase(key, "tags"u8))
        {
            ParseTags(value, tags);
        }
        else if (AsciiByteHelpers.EqualsIgnoreAsciiCase(key, "isblog"u8))
        {
            isBlog = AsciiByteHelpers.EqualsIgnoreAsciiCase(value, "true"u8);
        }
    }

    /// <summary>Splits the <c>Tags:</c> right-hand-side on commas.</summary>
    /// <param name="value">RHS of the <c>Tags</c> entry.</param>
    /// <param name="tags">Accumulator.</param>
    private static void ParseTags(ReadOnlySpan<byte> value, List<byte[]> tags)
    {
        if (value.IsEmpty)
        {
            return;
        }

        var cursor = 0;
        while (cursor < value.Length)
        {
            var rest = value[cursor..];
            var next = rest.IndexOf((byte)',');
            var slice = next < 0 ? rest : rest[..next];
            var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(slice);
            if (!trimmed.IsEmpty)
            {
                tags.Add(trimmed.ToArray());
            }

            if (next < 0)
            {
                break;
            }

            cursor += next + 1;
        }
    }

    /// <summary>Parses an ASCII <c>yyyy-MM-dd</c> span without going through a string allocation.</summary>
    /// <param name="value">UTF-8 candidate span.</param>
    /// <param name="parsed">The parsed date on success.</param>
    /// <returns>True when <paramref name="value"/> matches the format.</returns>
    private static bool TryParseDate(ReadOnlySpan<byte> value, out DateOnly parsed)
    {
        Span<char> chars = stackalloc char[value.Length];
        var written = Encoding.UTF8.GetChars(value, chars);
        return DateOnly.TryParseExact(chars[..written], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    /// <summary>Returns true when <paramref name="markdown"/> starts with a frontmatter fence.</summary>
    /// <param name="markdown">Source markdown.</param>
    /// <returns>True when the source starts with <c>---</c>.</returns>
    private static bool IsFrontmatterFence(ReadOnlySpan<byte> markdown) =>
        markdown.Length >= FrontmatterFenceLength
        && markdown[0] is (byte)'-'
        && markdown[1] is (byte)'-'
        && markdown[FrontmatterFenceLastIndex] is (byte)'-';

    /// <summary>Result of parsing.</summary>
    /// <param name="Title">Parsed title bytes (or empty).</param>
    /// <param name="Author">Parsed author bytes (or empty).</param>
    /// <param name="Published">Parsed publish date (or default).</param>
    /// <param name="Tags">Parsed tag list (possibly empty).</param>
    /// <param name="IsBlog">True when <c>IsBlog: true</c> was present.</param>
    /// <param name="BodyStartOffset">Byte offset within the original source where the markdown body starts.</param>
    public readonly record struct FrontmatterResult(
        byte[] Title,
        byte[] Author,
        DateOnly Published,
        byte[][] Tags,
        bool IsBlog,
        int BodyStartOffset);
}
