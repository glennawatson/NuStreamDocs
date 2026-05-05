// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tags;

/// <summary>Stateless helpers that emit the tags landing page and per-tag listing pages straight to byte buffers.</summary>
internal static class TagsIndexWriter
{
    /// <summary>Length of the <c>.md</c> source extension stripped before composing URLs.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Length of the replacement <c>.html</c> extension.</summary>
    private const int HtmlExtensionLength = 5;

    /// <summary>OR-mask that maps an ASCII uppercase letter to its lowercase form.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Initial-byte capacity hint for an emitted page; covers most pages without a resize.</summary>
    private const int PageInitialCapacity = 2 * 1024;

    /// <summary>Maps a source-relative path (e.g. <c>guide/intro.md</c>) to a UTF-8 URL byte array (e.g. <c>guide/intro.html</c>).</summary>
    /// <param name="relativePath">Source path relative to the docs root.</param>
    /// <returns>UTF-8 URL bytes; an empty array when <paramref name="relativePath"/> is empty.</returns>
    public static byte[] RelativePathToUrlPath(FilePath relativePath)
    {
        if (relativePath.IsEmpty)
        {
            return [];
        }

        ReadOnlySpan<char> span = relativePath;
        var endsWithMd = span.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var keep = endsWithMd ? span.Length - MarkdownExtensionLength : span.Length;
        var totalLength = keep + (endsWithMd ? HtmlExtensionLength : 0);
        var dst = new byte[totalLength];
        for (var i = 0; i < keep; i++)
        {
            var c = span[i];
            dst[i] = c is '\\' ? (byte)'/' : (byte)c;
        }

        if (endsWithMd)
        {
            ".html"u8.CopyTo(dst.AsSpan(keep));
        }

        return dst;
    }

    /// <summary>Emits the all-tags index plus one listing page per distinct tag.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="options">Plugin options controlling the output layout.</param>
    /// <param name="entries">Per-page tag occurrences collected during the build.</param>
    public static void Write(DirectoryPath outputRoot, in TagsOptions options, TagEntry[] entries)
    {
        if (entries.Length is 0)
        {
            return;
        }

        var grouped = GroupByTag(entries);
        var tagsDir = Path.Combine(outputRoot, options.OutputSubdirectory);
        Directory.CreateDirectory(tagsDir);

        using var rental = PageBuilderPool.Rent(PageInitialCapacity);
        var sink = rental.Writer;
        WriteIndexPage(sink, grouped);
        File.WriteAllBytes(Path.Combine(tagsDir, options.IndexFileName), sink.WrittenSpan);

        foreach (var pair in grouped)
        {
            sink.ResetWrittenCount();
            WriteTagPage(sink, pair.Key, pair.Value);
            var slug = SlugifyTag(pair.Key);
            File.WriteAllBytes(Path.Combine(tagsDir, BuildSlugFileName(slug)), sink.WrittenSpan);
        }
    }

    /// <summary>Builds a <c>{slug}.html</c> file name from ASCII slug bytes in a single allocation.</summary>
    /// <param name="slug">Slug bytes (ASCII alphanumeric / hyphen only, by construction of <see cref="SlugifyInto"/>).</param>
    /// <returns>The slug followed by the <c>.html</c> extension as a single allocated string.</returns>
    private static string BuildSlugFileName(byte[] slug) =>
        string.Create(slug.Length + HtmlExtensionLength, slug, static (dst, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = (char)src[i];
            }

            ".html".AsSpan().CopyTo(dst[src.Length..]);
        });

    /// <summary>Groups <paramref name="entries"/> by tag, yielding tags sorted alphabetically and pages within each tag sorted by URL.</summary>
    /// <param name="entries">Per-page entries.</param>
    /// <returns>Sorted byte-keyed tag → page list.</returns>
    private static SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> GroupByTag(TagEntry[] entries)
    {
        SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> map = new(ByteSequenceComparer.Instance);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (!map.TryGetValue(entry.Tag, out var bucket))
            {
                bucket = new List<(byte[], byte[])>(4);
                map[entry.Tag] = bucket;
            }

            bucket.Add((entry.PageUrl, entry.PageTitle));
        }

        foreach (var bucket in map.Values)
        {
            bucket.Sort(static (a, b) => ByteSequenceComparer.Instance.Compare(a.Url, b.Url));
        }

        return map;
    }

    /// <summary>Writes the all-tags landing page bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target sink.</param>
    /// <param name="grouped">Tag → page list, sorted.</param>
    private static void WriteIndexPage(IBufferWriter<byte> writer, SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> grouped)
    {
        writer.Write("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Tags</title>\n</head>\n<body>\n"u8);
        writer.Write("<main class=\"tags-index\">\n<h1>Tags</h1>\n<ul>\n"u8);
        foreach (var pair in grouped)
        {
            writer.Write("<li><a href=\""u8);
            writer.Write(SlugifyTag(pair.Key));
            writer.Write(".html\">"u8);
            XmlEntityEscaper.WriteEscaped(writer, pair.Key, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write("</a> <span class=\"tag-count\">("u8);
            Utf8StringWriter.WriteInt32(writer, pair.Value.Count);
            writer.Write(")</span></li>\n"u8);
        }

        writer.Write("</ul>\n</main>\n</body>\n</html>\n"u8);
    }

    /// <summary>Writes one per-tag listing page's bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target sink.</param>
    /// <param name="tag">UTF-8 tag display name.</param>
    /// <param name="pages">Pages carrying the tag.</param>
    private static void WriteTagPage(IBufferWriter<byte> writer, byte[] tag, List<(byte[] Url, byte[] Title)> pages)
    {
        writer.Write("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Tag: "u8);
        XmlEntityEscaper.WriteEscaped(writer, tag, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("</title>\n</head>\n<body>\n<main class=\"tags-page\">\n<h1>Tag: "u8);
        XmlEntityEscaper.WriteEscaped(writer, tag, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("</h1>\n<p><a href=\"index.html\">All tags</a></p>\n<ul>\n"u8);
        for (var i = 0; i < pages.Count; i++)
        {
            var (url, title) = pages[i];
            writer.Write("<li><a href=\"/"u8);
            writer.Write(url);
            writer.Write("\">"u8);
            XmlEntityEscaper.WriteEscaped(writer, title, XmlEntityEscaper.Mode.HtmlAttribute);
            writer.Write("</a></li>\n"u8);
        }

        writer.Write("</ul>\n</main>\n</body>\n</html>\n"u8);
    }

    /// <summary>Lowercases <paramref name="tag"/> and replaces non-alphanumeric ASCII runs with single hyphens for use as a filename.</summary>
    /// <param name="tag">UTF-8 tag display bytes.</param>
    /// <returns>UTF-8 filesystem-safe slug bytes; <c>"tag"</c> when the input has no slug-safe bytes.</returns>
    private static byte[] SlugifyTag(ReadOnlySpan<byte> tag)
    {
        if (tag.IsEmpty)
        {
            return [.. "tag"u8];
        }

        var stack = tag.Length <= 256 ? stackalloc byte[tag.Length] : new byte[tag.Length];
        var written = SlugifyInto(tag, stack);
        return written is 0 ? [.. "tag"u8] : stack[..written].ToArray();
    }

    /// <summary>Writes the slug form of <paramref name="tag"/> into <paramref name="dst"/> and returns the count.</summary>
    /// <param name="tag">UTF-8 source bytes.</param>
    /// <param name="dst">Destination span.</param>
    /// <returns>Number of bytes written.</returns>
    private static int SlugifyInto(ReadOnlySpan<byte> tag, Span<byte> dst)
    {
        var count = 0;
        var pendingHyphen = false;
        for (var i = 0; i < tag.Length; i++)
        {
            var b = tag[i];
            switch (b)
            {
                case >= (byte)'A' and <= (byte)'Z':
                    {
                        count = FlushHyphen(dst, count, pendingHyphen);
                        dst[count++] = (byte)(b | AsciiCaseBit);
                        pendingHyphen = false;
                        continue;
                    }

                case >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9':
                    {
                        count = FlushHyphen(dst, count, pendingHyphen);
                        dst[count++] = b;
                        pendingHyphen = false;
                        continue;
                    }

                default:
                    {
                        pendingHyphen = count is not 0;
                        break;
                    }
            }
        }

        return count;
    }

    /// <summary>Appends a queued hyphen when one is pending and the buffer is non-empty.</summary>
    /// <param name="dst">Destination span.</param>
    /// <param name="count">Current count.</param>
    /// <param name="pendingHyphen">Whether a hyphen is queued.</param>
    /// <returns>Updated count.</returns>
    private static int FlushHyphen(Span<byte> dst, int count, bool pendingHyphen)
    {
        if (!pendingHyphen || count is 0)
        {
            return count;
        }

        dst[count] = (byte)'-';
        return count + 1;
    }

    /// <summary>Ordinal byte-sequence comparer used for the sorted tag and url buckets.</summary>
    private sealed class ByteSequenceComparer : IComparer<byte[]>
    {
        /// <summary>Singleton instance.</summary>
        public static readonly ByteSequenceComparer Instance = new();

        /// <inheritdoc/>
        public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y);
    }
}
