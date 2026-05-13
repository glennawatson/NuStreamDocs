// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tags;

/// <summary>
/// Tags plugin. Walks every <c>*.md</c> page under the docs root, reads each page's
/// <c>tags:</c> frontmatter, and registers in-memory synthetic pages with
/// <see cref="BuildDiscoverContext.SyntheticPages"/> — a tags landing page
/// (<c>{OutputSubdirectory}/index.md</c>) plus one listing page per distinct tag
/// (<c>{OutputSubdirectory}/{slug}.md</c>). The pages flow through the regular render
/// pipeline (theme, search index, sitemap, canonical) without leaving any intermediate
/// files in the source folder.
/// </summary>
public sealed class TagsPlugin : IBuildDiscoverPlugin
{
    /// <summary>Plugin options.</summary>
    private readonly TagsOptions _options;

    /// <summary>Initializes a new instance of the <see cref="TagsPlugin"/> class with default options.</summary>
    public TagsPlugin()
        : this(TagsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagsPlugin"/> class with caller-supplied options.</summary>
    /// <param name="options">Options controlling the output subdirectory and index slug.</param>
    public TagsPlugin(in TagsOptions options) => _options = options;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "tags"u8;

    /// <inheritdoc/>
    public PluginPriority DiscoverPriority => new(PluginBand.Early);

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken)
    {
        if (context.InputRoot.IsEmpty || !Directory.Exists(context.InputRoot))
        {
            return;
        }

        var inputRoot = context.InputRoot;
        var tagsDir = inputRoot / _options.OutputSubdirectory;
        var collected = await CollectAsync(inputRoot, tagsDir, context.UseDirectoryUrls, cancellationToken)
            .ConfigureAwait(false);
        if (collected.Count is 0)
        {
            return;
        }

        // Build a *relative* DirectoryPath rooted at the output subdirectory; the synthetic
        // page enumerator joins it onto InputRoot when it yields the work item, so the page
        // appears at `{InputRoot}/{OutputSubdirectory}/...` virtually without ever touching disk.
        var virtualDir = DirectoryPath.FromString(_options.OutputSubdirectory);

        using var rental = PageBuilderPool.Rent(TagsCommon.PageInitialCapacity);
        var writer = rental.Writer;

        WriteIndexMarkdown(writer, collected);
        context.SyntheticPages.Add(new(virtualDir.UrlJoin("index.md"), writer.WrittenSpan.ToArray()));

        foreach (var pair in collected)
        {
            writer.ResetWrittenCount();
            WriteTagMarkdown(writer, pair.Key, pair.Value);
            var slug = TagsCommon.SlugifyTag(pair.Key);
            context.SyntheticPages.Add(new(
                virtualDir.UrlJoin(TagsCommon.BuildSlugFileName(slug, ".md"u8)),
                writer.WrittenSpan.ToArray()));
        }
    }

    /// <summary>Walks <paramref name="inputRoot"/> and groups pages by their <c>tags:</c> frontmatter.</summary>
    /// <param name="inputRoot">Absolute docs root.</param>
    /// <param name="tagsDir">Output directory; legacy on-disk files under it are skipped so a stale pre-virtual-page-pipeline tags folder cannot re-feed itself into a fresh build.</param>
    /// <param name="useDirectoryUrls">Build-pipeline directory-URL mode flag, forwarded into per-page URL composition so emitted hrefs match the pages the build will write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tag → list of <c>(url, title)</c> pairs, sorted by tag and by URL within each bucket.</returns>
    private static async ValueTask<SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>>> CollectAsync(
        DirectoryPath inputRoot,
        DirectoryPath tagsDir,
        bool useDirectoryUrls,
        CancellationToken cancellationToken)
    {
        const int InitialCapacity = 4;
        SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> map = new(ByteArrayComparer.Instance);
        var files = Directory.GetFiles(inputRoot, "*.md", SearchOption.AllDirectories);
        for (var i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = (FilePath)files[i];
            if (IsUnder(path, tagsDir))
            {
                continue;
            }

            var bytes = await path.ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
            var tags = TagsFrontmatterReader.Read(bytes);
            if (tags.Length is 0)
            {
                continue;
            }

            var relative = (FilePath)Path.GetRelativePath(inputRoot.Value, path.Value);
            var url = Utf8MarkdownUrl.FromRelativePath(relative, useDirectoryUrls);
            var title = ExtractMarkdownTitle(bytes, url);

            for (var t = 0; t < tags.Length; t++)
            {
                if (!map.TryGetValue(tags[t], out var bucket))
                {
                    bucket = new(InitialCapacity);
                    map[tags[t]] = bucket;
                }

                bucket.Add((url, title));
            }
        }

        foreach (var bucket in map.Values)
        {
            bucket.Sort(static (a, b) => ByteArrayComparer.Instance.Compare(a.Url, b.Url));
        }

        return map;
    }

    /// <summary>Determines whether <paramref name="path"/> is under <paramref name="directory"/>.</summary>
    /// <param name="path">Absolute candidate path.</param>
    /// <param name="directory">Absolute directory.</param>
    /// <returns>True for descendants; false otherwise.</returns>
    private static bool IsUnder(in FilePath path, in DirectoryPath directory)
    {
        if (directory.IsEmpty)
        {
            return false;
        }

        var dirSpan = directory.AsSpan();
        var pathSpan = path.AsSpan();
        if (pathSpan.Length <= dirSpan.Length)
        {
            return false;
        }

        if (!pathSpan.StartsWith(dirSpan, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var next = pathSpan[dirSpan.Length];
        return next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar;
    }

    /// <summary>Pulls the first ATX <c>#</c> heading text out of <paramref name="markdownBytes"/> for use as the page title; falls back to <paramref name="fallback"/>.</summary>
    /// <param name="markdownBytes">UTF-8 markdown source (frontmatter + body).</param>
    /// <param name="fallback">Default UTF-8 title bytes when no heading is found.</param>
    /// <returns>The page title bytes.</returns>
    private static byte[] ExtractMarkdownTitle(ReadOnlySpan<byte> markdownBytes, byte[] fallback)
    {
        var bodyStart = 0;
        if (YamlByteScanner.TryFindFrontmatter(markdownBytes, out _, out var afterFrontmatter))
        {
            bodyStart = afterFrontmatter;
        }

        var cursor = bodyStart;
        while (cursor < markdownBytes.Length)
        {
            var lineEnd = Utf8LineSpan.LfLineEnd(markdownBytes, cursor);
            var line = markdownBytes[cursor..lineEnd];
            var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(line);
            if (trimmed is [(byte)'#', (byte)' ', ..])
            {
                var titleSpan = AsciiByteHelpers.TrimAsciiWhitespace(trimmed[2..]);
                return titleSpan.IsEmpty ? fallback : titleSpan.ToArray();
            }

            cursor = lineEnd;
        }

        return fallback;
    }

    /// <summary>Writes the all-tags landing markdown into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target sink.</param>
    /// <param name="grouped">Tag → page list, sorted.</param>
    private static void WriteIndexMarkdown(
        IBufferWriter<byte> writer,
        SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> grouped)
    {
        writer.Write("# Tags\n\n"u8);
        foreach (var pair in grouped)
        {
            writer.Write("- ["u8);
            writer.Write(pair.Key);
            writer.Write("]("u8);
            writer.Write(TagsCommon.SlugifyTag(pair.Key));
            writer.Write(".md) ("u8);
            Utf8StringWriter.WriteInt32(writer, pair.Value.Count);
            writer.Write(")\n"u8);
        }
    }

    /// <summary>Writes one per-tag listing markdown into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target sink.</param>
    /// <param name="tag">UTF-8 tag display name.</param>
    /// <param name="pages">Pages carrying the tag.</param>
    private static void WriteTagMarkdown(IBufferWriter<byte> writer, byte[] tag, List<(byte[] Url, byte[] Title)> pages)
    {
        writer.Write("# Tag: "u8);
        writer.Write(tag);
        writer.Write("\n\n[All tags](index.md)\n\n"u8);
        for (var i = 0; i < pages.Count; i++)
        {
            var (url, title) = pages[i];
            writer.Write("- ["u8);
            writer.Write(title);
            writer.Write("](/"u8);
            writer.Write(url);
            writer.Write(")\n"u8);
        }
    }
}
