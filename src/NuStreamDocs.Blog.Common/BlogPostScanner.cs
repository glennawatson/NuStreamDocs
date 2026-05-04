// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Common;

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
    public static BlogPost[] Scan(DirectoryPath postsRoot, DirectoryPath docsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postsRoot.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(docsRoot.Value);
        if (!postsRoot.Exists())
        {
            return [];
        }

        var posts = new List<BlogPost>(64);
        foreach (var path in postsRoot.EnumerateFiles("*.md", SearchOption.TopDirectoryOnly))
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
    private static BlogPost? TryReadPost(FilePath absolutePath, DirectoryPath docsRoot)
    {
        var fileName = absolutePath.FileNameWithoutExtension;
        if (fileName.Length <= DatePrefixLength
            || fileName[YearMonthSeparatorIndex] != '-'
            || fileName[MonthDaySeparatorIndex] != '-'
            || fileName[DateSlugSeparatorIndex] != '-')
        {
            return null;
        }

        var datePart = fileName.AsSpan(0, DateLength);
        if (!DateOnly.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
        {
            return null;
        }

        var slug = fileName[DatePrefixLength..];
        var bytes = absolutePath.ReadAllBytes();
        var fm = WyamFrontmatterReader.Parse(bytes);

        var published = fm.Published == default ? fileDate : fm.Published;
        var titleBytes = fm.Title is [_, ..] ? fm.Title : HumanizeBytes(slug);
        var excerptBytes = ExtractExcerpt(bytes, fm.BodyStartOffset);
        var relativePath = NormalizeRelativePath(Path.GetRelativePath(docsRoot.Value, absolutePath.Value));

        return new(
            relativePath,
            Encoding.UTF8.GetBytes(slug),
            titleBytes,
            fm.Author,
            published,
            fm.Tags,
            excerptBytes);
    }

    /// <summary>Pulls the first non-empty paragraph from the body as a plain-text excerpt.</summary>
    /// <param name="bytes">UTF-8 bytes of the full markdown source.</param>
    /// <param name="bodyOffset">Byte offset where the body begins.</param>
    /// <returns>Excerpt with trailing newlines trimmed; empty when no paragraph is found.</returns>
    private static byte[] ExtractExcerpt(byte[] bytes, int bodyOffset)
    {
        if (bodyOffset >= bytes.Length)
        {
            return [];
        }

        var body = bytes.AsSpan(bodyOffset);
        while (!body.IsEmpty)
        {
            var lineEnd = body.IndexOf((byte)'\n');
            if (lineEnd < 0)
            {
                lineEnd = body.Length;
            }

            var line = body[..lineEnd].TrimEnd((byte)'\r').Trim((byte)' ').Trim((byte)'\t');
            if (!line.IsEmpty && !IsAtxHeading(line) && !IsFrontmatterFence(line))
            {
                return line.ToArray();
            }

            if (lineEnd >= body.Length)
            {
                break;
            }

            body = body[(lineEnd + 1)..];
        }

        return [];
    }

    /// <summary>Title-cases <c>my-cool-post</c> into <c>My Cool Post</c> as a fallback when no Title frontmatter is present, emitting UTF-8 bytes.</summary>
    /// <param name="slug">Hyphen-separated ASCII slug (filename stem).</param>
    /// <returns>Spaced title-case rendering as UTF-8 bytes.</returns>
    private static byte[] HumanizeBytes(string slug)
    {
        var outputLength = GetHumanizedLength(slug);
        if (outputLength is 0)
        {
            return [];
        }

        var dst = new byte[outputLength];
        var write = 0;
        var titleCaseNext = true;
        var pendingSpace = false;
        for (var i = 0; i < slug.Length; i++)
        {
            var c = slug[i];
            if (c is '-')
            {
                pendingSpace = write is not 0;
                titleCaseNext = true;
                continue;
            }

            if (pendingSpace)
            {
                dst[write++] = (byte)' ';
                pendingSpace = false;
            }

            // Slug chars are ASCII (filename stem after the YYYY-MM-DD- prefix); narrow to byte directly.
            dst[write++] = titleCaseNext ? (byte)char.ToUpperInvariant(c) : (byte)c;
            titleCaseNext = false;
        }

        return dst;
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
    private static bool IsAtxHeading(in ReadOnlySpan<byte> line) => line is [(byte)'#', ..];

    /// <summary>Returns true when <paramref name="line"/> is exactly a frontmatter fence.</summary>
    /// <param name="line">Candidate line.</param>
    /// <returns>True when the line is exactly <c>---</c>.</returns>
    private static bool IsFrontmatterFence(in ReadOnlySpan<byte> line) =>
        line.Length == FrontmatterFenceLength
        && line[0] is (byte)'-'
        && line[1] is (byte)'-'
        && line[FrontmatterFenceLastIndex] is (byte)'-';

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
