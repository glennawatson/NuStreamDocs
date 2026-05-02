// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Walks a flat directory of <c>YYYY-MM-DD-slug.md</c> blog posts and
/// returns the parsed metadata.
/// </summary>
public static class BlogPostScanner
{
    /// <summary>The expected filename date prefix length: <c>YYYY-MM-DD-</c>.</summary>
    private const int DatePrefixLength = 11;

    /// <summary>Index of the year/month separator inside <c>YYYY-MM-DD</c>.</summary>
    private const int YearMonthSeparatorIndex = 4;

    /// <summary>Index of the month/day separator inside <c>YYYY-MM-DD</c>.</summary>
    private const int MonthDaySeparatorIndex = 7;

    /// <summary>Index of the trailing hyphen between the date and the slug.</summary>
    private const int DateSlugSeparatorIndex = 10;

    /// <summary>Length of the <c>YYYY-MM-DD</c> date itself, excluding the trailing hyphen.</summary>
    private const int DateLength = 10;

    /// <summary>Length of the frontmatter fence (<c>---</c>).</summary>
    private const int FrontmatterFenceLength = 3;

    /// <summary>Index of the third dash in the frontmatter fence.</summary>
    private const int FrontmatterFenceLastIndex = 2;

    /// <summary>Scans <paramref name="postsRoot"/> for Wyam-style blog posts.</summary>
    /// <param name="postsRoot">Absolute path to the directory holding the post files.</param>
    /// <param name="docsRoot">Absolute path to the docs root (used for the post's relative path).</param>
    /// <returns>Parsed posts, ordered by publish date descending.</returns>
    public static BlogPost[] Scan(string postsRoot, string docsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(docsRoot);
        if (!Directory.Exists(postsRoot))
        {
            return [];
        }

        var posts = new List<BlogPost>(64);
        foreach (var path in Directory.EnumerateFiles(postsRoot, "*.md", SearchOption.TopDirectoryOnly))
        {
            var post = TryReadPost(path, docsRoot);
            if (post is not null)
            {
                posts.Add(post);
            }
        }

        BlogPost[] array = [.. posts];
        Array.Sort(array, BlogPostByPublishedDescending.Instance);
        return array;
    }

    /// <summary>Reads one post file and returns its <see cref="BlogPost"/>; returns null when the file is not a recognizable post.</summary>
    /// <param name="absolutePath">Absolute path to the post file.</param>
    /// <param name="docsRoot">Absolute docs root.</param>
    /// <returns>The parsed post, or null when the file isn't a Wyam-style post.</returns>
    private static BlogPost? TryReadPost(string absolutePath, string docsRoot)
    {
        var fileName = Path.GetFileNameWithoutExtension(absolutePath);
        if (fileName.Length <= DatePrefixLength
            || fileName[YearMonthSeparatorIndex] != '-'
            || fileName[MonthDaySeparatorIndex] != '-'
            || fileName[DateSlugSeparatorIndex] != '-')
        {
            return null;
        }

        var datePart = fileName[..DateLength];
        if (!DateOnly.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
        {
            return null;
        }

        var slug = fileName[DatePrefixLength..];
        var markdown = File.ReadAllText(absolutePath);
        var fm = WyamFrontmatterReader.Parse(markdown);

        var published = fm.Published == default ? fileDate : fm.Published;
        var title = string.IsNullOrEmpty(fm.Title) ? Humanize(slug) : fm.Title;
        var excerpt = ExtractExcerpt(markdown, fm.BodyStartOffset);
        var relativePath = NormalizeRelativePath(Path.GetRelativePath(docsRoot, absolutePath));
        var tags = fm.Tags ?? [];

        return new(relativePath, slug, title, fm.Author, published, tags, excerpt);
    }

    /// <summary>Pulls the first non-empty paragraph from the body as a plain-text excerpt.</summary>
    /// <param name="markdown">Full source.</param>
    /// <param name="bodyOffset">Byte offset where the body begins.</param>
    /// <returns>Excerpt with trailing newlines trimmed; empty when no paragraph is found.</returns>
    private static string ExtractExcerpt(string markdown, int bodyOffset)
    {
        if (bodyOffset >= markdown.Length)
        {
            return string.Empty;
        }

        var body = markdown.AsSpan(bodyOffset);
        while (!body.IsEmpty)
        {
            var lineEnd = body.IndexOf('\n');
            if (lineEnd < 0)
            {
                lineEnd = body.Length;
            }

            var line = body[..lineEnd].TrimEnd('\r').Trim();
            if (!line.IsEmpty && !IsAtxHeading(line) && !IsFrontmatterFence(line))
            {
                return line.ToString();
            }

            if (lineEnd >= body.Length)
            {
                break;
            }

            body = body[(lineEnd + 1)..];
        }

        return string.Empty;
    }

    /// <summary>Title-cases <c>my-cool-post</c> into <c>My Cool Post</c> as a fallback when no Title frontmatter is present.</summary>
    /// <param name="slug">Hyphen-separated slug.</param>
    /// <returns>Spaced title-case rendering.</returns>
    private static string Humanize(string slug)
    {
        var outputLength = GetHumanizedLength(slug);
        if (outputLength is 0)
        {
            return string.Empty;
        }

        return string.Create(outputLength, slug, static (dst, source) => WriteHumanized(dst, source));
    }

    /// <summary>Counts the characters needed for the humanized title.</summary>
    /// <param name="slug">Hyphen-separated slug.</param>
    /// <returns>Output length.</returns>
    private static int GetHumanizedLength(string slug)
    {
        var outputLength = 0;
        var pendingSpace = false;
        for (var i = 0; i < slug.Length; i++)
        {
            if (slug[i] is '-')
            {
                pendingSpace = outputLength is not 0;
                continue;
            }

            if (pendingSpace)
            {
                outputLength++;
                pendingSpace = false;
            }

            outputLength++;
        }

        return outputLength;
    }

    /// <summary>Writes the humanized title into <paramref name="destination"/>.</summary>
    /// <param name="destination">Destination span.</param>
    /// <param name="source">Hyphen-separated slug.</param>
    private static void WriteHumanized(Span<char> destination, string source)
    {
        var write = 0;
        var titleCaseNext = true;
        var pendingSpace = false;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c is '-')
            {
                pendingSpace = write is not 0;
                titleCaseNext = true;
                continue;
            }

            if (pendingSpace)
            {
                destination[write++] = ' ';
                pendingSpace = false;
            }

            destination[write++] = titleCaseNext ? char.ToUpperInvariant(c) : c;
            titleCaseNext = false;
        }
    }

    /// <summary>Normalizes a source-relative path to forward slashes.</summary>
    /// <param name="relativePath">Path to normalize.</param>
    /// <returns>Forward-slashed path.</returns>
    private static string NormalizeRelativePath(string relativePath)
    {
        if (!relativePath.AsSpan().Contains('\\'))
        {
            return relativePath;
        }

        return string.Create(relativePath.Length, relativePath, static (dst, state) =>
        {
            for (var i = 0; i < state.Length; i++)
            {
                dst[i] = state[i] is '\\' ? '/' : state[i];
            }
        });
    }

    /// <summary>Returns true when <paramref name="line"/> starts an ATX heading.</summary>
    /// <param name="line">Candidate line.</param>
    /// <returns>True when the line starts with <c>#</c>.</returns>
    private static bool IsAtxHeading(ReadOnlySpan<char> line) => line is ['#', ..];

    /// <summary>Returns true when <paramref name="line"/> is exactly a frontmatter fence.</summary>
    /// <param name="line">Candidate line.</param>
    /// <returns>True when the line is exactly <c>---</c>.</returns>
    private static bool IsFrontmatterFence(ReadOnlySpan<char> line) =>
        line.Length == FrontmatterFenceLength
        && line[0] is '-'
        && line[1] is '-'
        && line[FrontmatterFenceLastIndex] is '-';

    /// <summary>Comparer that orders posts by <see cref="BlogPost.Published"/> descending; cached as a singleton to avoid per-call lambda allocations.</summary>
    private sealed class BlogPostByPublishedDescending : IComparer<BlogPost>
    {
        /// <summary>Singleton instance.</summary>
        public static readonly BlogPostByPublishedDescending Instance = new();

        /// <inheritdoc/>
        public int Compare(BlogPost? x, BlogPost? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return 1;
            }

            if (y is null)
            {
                return -1;
            }

            return y.Published.CompareTo(x.Published);
        }
    }
}
