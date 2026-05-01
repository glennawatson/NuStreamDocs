// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Reads Wyam-style YAML frontmatter from a markdown source string.
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
    /// <param name="markdown">Full markdown source.</param>
    /// <returns>The parsed frontmatter values plus the byte offset where the body begins.</returns>
    public static FrontmatterResult Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (!IsFrontmatterFence(markdown))
        {
            return new(string.Empty, string.Empty, default, [], false, 0);
        }

        var firstLineEnd = markdown.IndexOf('\n', FrontmatterFenceLength);
        if (firstLineEnd < 0)
        {
            return new(string.Empty, string.Empty, default, [], false, 0);
        }

        var bodyStart = -1;
        var cursor = firstLineEnd + 1;
        var title = string.Empty;
        var author = string.Empty;
        var published = default(DateOnly);
        var tags = new List<string>(4);
        var isBlog = false;

        while (cursor < markdown.Length)
        {
            var lineEnd = markdown.IndexOf('\n', cursor);
            if (lineEnd < 0)
            {
                lineEnd = markdown.Length;
            }

            var line = markdown.AsSpan(cursor, lineEnd - cursor).TrimEnd('\r');
            if (IsFrontmatterFence(line))
            {
                bodyStart = lineEnd + 1;
                break;
            }

            ParseLine(line, ref title, ref author, ref published, tags, ref isBlog);
            cursor = lineEnd + 1;
        }

        if (bodyStart < 0)
        {
            return new(string.Empty, string.Empty, default, [], false, 0);
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
        ReadOnlySpan<char> line,
        ref string title,
        ref string author,
        ref DateOnly published,
        List<string> tags,
        ref bool isBlog)
    {
        var sep = line.IndexOf(':');
        if (sep <= 0)
        {
            return;
        }

        var key = line[..sep].Trim();
        var value = line[(sep + 1)..].Trim();

        if (key.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            title = value.ToString();
        }
        else if (key.Equals("Author", StringComparison.OrdinalIgnoreCase))
        {
            author = value.ToString();
        }
        else if (key.Equals("Published", StringComparison.OrdinalIgnoreCase))
        {
            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                published = parsed;
            }
        }
        else if (key.Equals("Tags", StringComparison.OrdinalIgnoreCase))
        {
            ParseTags(value, tags);
        }
        else if (key.Equals("IsBlog", StringComparison.OrdinalIgnoreCase))
        {
            isBlog = value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Splits the <c>Tags:</c> right-hand-side on commas.</summary>
    /// <param name="value">RHS of the <c>Tags</c> entry.</param>
    /// <param name="tags">Accumulator.</param>
    private static void ParseTags(ReadOnlySpan<char> value, List<string> tags)
    {
        if (value.IsEmpty)
        {
            return;
        }

        var cursor = 0;
        while (cursor < value.Length)
        {
            var next = value[cursor..].IndexOf(',');
            var slice = next < 0 ? value[cursor..] : value.Slice(cursor, next);
            var trimmed = slice.Trim();
            if (!trimmed.IsEmpty)
            {
                tags.Add(trimmed.ToString());
            }

            if (next < 0)
            {
                break;
            }

            cursor += next + 1;
        }
    }

    /// <summary>Returns true when <paramref name="markdown"/> starts with a frontmatter fence.</summary>
    /// <param name="markdown">Source markdown.</param>
    /// <returns>True when the source starts with <c>---</c>.</returns>
    private static bool IsFrontmatterFence(string markdown) =>
        markdown.Length >= FrontmatterFenceLength
        && markdown[0] is '-'
        && markdown[1] is '-'
        && markdown[FrontmatterFenceLastIndex] is '-';

    /// <summary>Returns true when <paramref name="line"/> is exactly a frontmatter fence.</summary>
    /// <param name="line">Candidate line.</param>
    /// <returns>True when the line is exactly <c>---</c>.</returns>
    private static bool IsFrontmatterFence(ReadOnlySpan<char> line) =>
        line.Length == FrontmatterFenceLength
        && line[0] is '-'
        && line[1] is '-'
        && line[FrontmatterFenceLastIndex] is '-';

    /// <summary>Result of parsing.</summary>
    /// <param name="Title">Parsed title (or empty).</param>
    /// <param name="Author">Parsed author (or empty).</param>
    /// <param name="Published">Parsed publish date (or default).</param>
    /// <param name="Tags">Parsed tag list (possibly empty).</param>
    /// <param name="IsBlog">True when <c>IsBlog: true</c> was present.</param>
    /// <param name="BodyStartOffset">Byte offset within the original source where the markdown body starts.</param>
    public readonly record struct FrontmatterResult(
        string Title,
        string Author,
        DateOnly Published,
        string[] Tags,
        bool IsBlog,
        int BodyStartOffset);
}
