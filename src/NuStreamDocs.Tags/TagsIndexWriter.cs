// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tags;

/// <summary>Stateless helpers that emit the tags landing page and per-tag listing pages straight to byte buffers.</summary>
internal static class TagsIndexWriter
{
    /// <summary>Maps a source-relative path (e.g. <c>guide/intro.md</c>) to a UTF-8 URL byte array (e.g. <c>guide/intro.html</c>).</summary>
    /// <param name="relativePath">Source path relative to the docs root.</param>
    /// <returns>UTF-8 URL bytes; an empty array when <paramref name="relativePath"/> is empty.</returns>
    public static byte[] RelativePathToUrlPath(FilePath relativePath) =>
        relativePath.IsEmpty ? [] : TagsCommon.MdRelativePathToHtmlUrlBytes(relativePath);

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

        using var rental = PageBuilderPool.Rent(TagsCommon.PageInitialCapacity);
        var sink = rental.Writer;
        WriteIndexPage(sink, grouped);
        File.WriteAllBytes(Path.Combine(tagsDir, options.IndexFileName), sink.WrittenSpan);

        foreach (var pair in grouped)
        {
            sink.ResetWrittenCount();
            WriteTagPage(sink, pair.Key, pair.Value);
            var slug = TagsCommon.SlugifyTag(pair.Key);
            File.WriteAllBytes(Path.Combine(tagsDir, TagsCommon.BuildSlugFileName(slug, ".html"u8)), sink.WrittenSpan);
        }
    }

    /// <summary>Groups <paramref name="entries"/> by tag, yielding tags sorted alphabetically and pages within each tag sorted by URL.</summary>
    /// <param name="entries">Per-page entries.</param>
    /// <returns>Sorted byte-keyed tag → page list.</returns>
    private static SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> GroupByTag(TagEntry[] entries)
    {
        SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> map = new(ByteArrayComparer.Instance);
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
            bucket.Sort(static (a, b) => ByteArrayComparer.Instance.Compare(a.Url, b.Url));
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
            writer.Write(TagsCommon.SlugifyTag(pair.Key));
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
}
