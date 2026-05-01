// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tags;

/// <summary>Stateless helpers that emit the tags landing page and per-tag listing pages straight to byte buffers.</summary>
internal static class TagsIndexWriter
{
    /// <summary>Length of the <c>.md</c> source extension stripped before composing URLs.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Length of the replacement <c>.html</c> extension.</summary>
    private const int HtmlExtensionLength = 5;

    /// <summary>ASCII offset to convert an upper-case letter to lower-case.</summary>
    private const int AsciiUpperToLowerOffset = 32;

    /// <summary>Initial-byte capacity hint for an emitted page; covers most pages without a resize.</summary>
    private const int PageInitialCapacity = 2 * 1024;

    /// <summary>Maps a source-relative path (e.g. <c>guide/intro.md</c>) to a URL path (e.g. <c>guide/intro.html</c>).</summary>
    /// <param name="relativePath">Source path relative to the docs root.</param>
    /// <returns>URL-shaped path; empty when input is unusable.</returns>
    public static string RelativePathToUrlPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return string.Empty;
        }

        var span = relativePath.AsSpan();
        var endsWithMd = span.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var keep = endsWithMd ? span.Length - MarkdownExtensionLength : span.Length;
        var totalLength = keep + (endsWithMd ? HtmlExtensionLength : 0);
        return string.Create(totalLength, (relativePath, keep, endsWithMd), static (dst, state) =>
        {
            var src = state.relativePath.AsSpan(0, state.keep);
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = src[i] is '\\' ? '/' : src[i];
            }

            if (!state.endsWithMd)
            {
                return;
            }

            ".html".AsSpan().CopyTo(dst[state.keep..]);
        });
    }

    /// <summary>Emits the all-tags index plus one listing page per distinct tag.</summary>
    /// <param name="outputRoot">Absolute path to the site output directory.</param>
    /// <param name="options">Plugin options controlling the output layout.</param>
    /// <param name="entries">Per-page tag occurrences collected during the build.</param>
    public static void Write(string outputRoot, in TagsOptions options, TagEntry[] entries)
    {
        if (entries.Length is 0)
        {
            return;
        }

        var grouped = GroupByTag(entries);
        var tagsDir = Path.Combine(outputRoot, options.OutputSubdirectory);
        Directory.CreateDirectory(tagsDir);

        var sink = new ArrayBufferWriter<byte>(PageInitialCapacity);
        WriteIndexPage(sink, grouped);
        File.WriteAllBytes(Path.Combine(tagsDir, options.IndexFileName), sink.WrittenSpan);

        foreach (var (tag, pages) in grouped)
        {
            sink.ResetWrittenCount();
            WriteTagPage(sink, tag, pages);
            File.WriteAllBytes(Path.Combine(tagsDir, SlugifyTag(tag) + ".html"), sink.WrittenSpan);
        }
    }

    /// <summary>Groups <paramref name="entries"/> by tag, yielding tags sorted alphabetically and pages within each tag sorted by URL.</summary>
    /// <param name="entries">Per-page entries.</param>
    /// <returns>Sorted tag → page list.</returns>
    private static SortedDictionary<string, List<(string Url, string Title)>> GroupByTag(TagEntry[] entries)
    {
        var map = new SortedDictionary<string, List<(string Url, string Title)>>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (!map.TryGetValue(entry.Tag, out var bucket))
            {
                bucket = new List<(string, string)>(4);
                map[entry.Tag] = bucket;
            }

            bucket.Add((entry.PageUrl, entry.PageTitle));
        }

        foreach (var bucket in map.Values)
        {
            bucket.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Url, b.Url));
        }

        return map;
    }

    /// <summary>Writes the all-tags landing page bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target sink.</param>
    /// <param name="grouped">Tag → page list, sorted.</param>
    private static void WriteIndexPage(IBufferWriter<byte> writer, SortedDictionary<string, List<(string Url, string Title)>> grouped)
    {
        writer.Write("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Tags</title>\n</head>\n<body>\n"u8);
        writer.Write("<main class=\"tags-index\">\n<h1>Tags</h1>\n<ul>\n"u8);
        foreach (var (tag, pages) in grouped)
        {
            writer.Write("<li><a href=\""u8);
            Utf8StringWriter.Write(writer, SlugifyTag(tag));
            writer.Write(".html\">"u8);
            WriteEscaped(writer, tag);
            writer.Write("</a> <span class=\"tag-count\">("u8);
            Utf8StringWriter.WriteInt32(writer, pages.Count);
            writer.Write(")</span></li>\n"u8);
        }

        writer.Write("</ul>\n</main>\n</body>\n</html>\n"u8);
    }

    /// <summary>Writes one per-tag listing page's bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target sink.</param>
    /// <param name="tag">Tag display name.</param>
    /// <param name="pages">Pages carrying the tag.</param>
    private static void WriteTagPage(IBufferWriter<byte> writer, string tag, List<(string Url, string Title)> pages)
    {
        writer.Write("<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Tag: "u8);
        WriteEscaped(writer, tag);
        writer.Write("</title>\n</head>\n<body>\n<main class=\"tags-page\">\n<h1>Tag: "u8);
        WriteEscaped(writer, tag);
        writer.Write("</h1>\n<p><a href=\"index.html\">All tags</a></p>\n<ul>\n"u8);
        for (var i = 0; i < pages.Count; i++)
        {
            var (url, title) = pages[i];
            writer.Write("<li><a href=\"/"u8);
            Utf8StringWriter.Write(writer, url);
            writer.Write("\">"u8);
            WriteEscaped(writer, title);
            writer.Write("</a></li>\n"u8);
        }

        writer.Write("</ul>\n</main>\n</body>\n</html>\n"u8);
    }

    /// <summary>Writes <paramref name="text"/> as UTF-8 with the four HTML-special chars expanded to their entities.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="text">Source text.</param>
    private static void WriteEscaped(IBufferWriter<byte> writer, string text)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);
        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(text, rented);
            XmlEntityEscaper.WriteEscaped(writer, rented.AsSpan(0, written), XmlEntityEscaper.Mode.HtmlAttribute);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Lowercases <paramref name="tag"/> and replaces non-alphanumeric runs with single hyphens for use as a filename.</summary>
    /// <param name="tag">Tag display name.</param>
    /// <returns>Filesystem-safe slug.</returns>
    private static string SlugifyTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return "tag";
        }

        var buffer = tag.Length <= 256 ? stackalloc char[tag.Length] : new char[tag.Length];
        var written = SlugifyInto(tag, buffer);
        return written is 0 ? "tag" : new(buffer[..written]);
    }

    /// <summary>Writes the slug form of <paramref name="tag"/> into <paramref name="dst"/> and returns the count.</summary>
    /// <param name="tag">Source.</param>
    /// <param name="dst">Destination.</param>
    /// <returns>Number of chars written.</returns>
    private static int SlugifyInto(string tag, in Span<char> dst)
    {
        var count = 0;
        var pendingHyphen = false;
        for (var i = 0; i < tag.Length; i++)
        {
            var c = tag[i];
            if (c is >= 'A' and <= 'Z')
            {
                count = FlushHyphen(dst, count, pendingHyphen);
                dst[count++] = (char)(c + AsciiUpperToLowerOffset);
                pendingHyphen = false;
                continue;
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                count = FlushHyphen(dst, count, pendingHyphen);
                dst[count++] = c;
                pendingHyphen = false;
                continue;
            }

            pendingHyphen = count is not 0;
        }

        return count;
    }

    /// <summary>Appends a queued hyphen when one is pending and the buffer is non-empty.</summary>
    /// <param name="dst">Destination.</param>
    /// <param name="count">Current count.</param>
    /// <param name="pendingHyphen">Whether a hyphen is queued.</param>
    /// <returns>Updated count.</returns>
    private static int FlushHyphen(in Span<char> dst, int count, bool pendingHyphen)
    {
        if (!pendingHyphen || count is 0)
        {
            return count;
        }

        dst[count] = '-';
        return count + 1;
    }
}
