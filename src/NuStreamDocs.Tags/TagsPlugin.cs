// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tags;

/// <summary>
/// Tags plugin. Walks every <c>*.md</c> page under the docs root,
/// reads each page's <c>tags:</c> frontmatter, and synthesizes virtual
/// markdown pages — a tags landing page (<c>{OutputSubdirectory}/index.md</c>)
/// plus one listing page per distinct tag (<c>{OutputSubdirectory}/{slug}.md</c>) —
/// so the page enumerator picks them up alongside author content.
/// </summary>
public sealed class TagsPlugin : IBuildDiscoverPlugin
{
    /// <summary>OR-mask that maps an ASCII uppercase letter to its lowercase form.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Initial-byte capacity hint for an emitted page; covers most pages without a resize.</summary>
    private const int PageInitialCapacity = 2 * 1024;

    /// <summary>Length of the <c>.md</c> source extension stripped before composing slugs.</summary>
    private const int MarkdownExtensionLength = 3;

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
        var collected = await CollectAsync(inputRoot, tagsDir, cancellationToken).ConfigureAwait(false);
        if (collected.Count is 0)
        {
            return;
        }

        Directory.CreateDirectory(tagsDir);

        using var rental = PageBuilderPool.Rent(PageInitialCapacity);
        var sink = rental.Writer;

        WriteIndexMarkdown(sink, collected);
        await File.WriteAllBytesAsync(tagsDir.File("index.md"), sink.WrittenMemory, cancellationToken).ConfigureAwait(false);

        foreach (var pair in collected)
        {
            sink.ResetWrittenCount();
            WriteTagMarkdown(sink, pair.Key, pair.Value);
            var slug = SlugifyTag(pair.Key);
            var fileName = BuildSlugMarkdownFileName(slug);
            await File.WriteAllBytesAsync(tagsDir.File(fileName), sink.WrittenMemory, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Walks <paramref name="inputRoot"/> for <c>*.md</c> files, parses the <c>tags:</c> frontmatter on each, and groups <c>(pageUrl, pageTitle)</c> pairs by tag.</summary>
    /// <param name="inputRoot">Absolute docs root.</param>
    /// <param name="tagsDir">Output directory for generated tag pages; files under this directory are skipped during the walk to avoid feedback loops on incremental rebuilds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tag → list of <c>(url, title)</c> pairs, with tags sorted alphabetically and pages sorted by URL within each bucket.</returns>
    private static async ValueTask<SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>>> CollectAsync(
        DirectoryPath inputRoot,
        DirectoryPath tagsDir,
        CancellationToken cancellationToken)
    {
        var map = new SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>>(ByteSequenceComparer.Instance);
        var files = Directory.GetFiles(inputRoot, "*.md", SearchOption.AllDirectories);
        for (var i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = files[i];
            if (IsUnder(path, tagsDir))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var tags = TagsFrontmatterReader.Read(bytes);
            if (tags.Length is 0)
            {
                continue;
            }

            var relative = Path.GetRelativePath(inputRoot.Value, path);
            var url = ToHtmlUrlBytes(relative);
            var title = ExtractMarkdownTitle(bytes, fallback: url);

            for (var t = 0; t < tags.Length; t++)
            {
                if (!map.TryGetValue(tags[t], out var bucket))
                {
                    bucket = new List<(byte[] Url, byte[] Title)>(4);
                    map[tags[t]] = bucket;
                }

                bucket.Add((url, title));
            }
        }

        foreach (var bucket in map.Values)
        {
            bucket.Sort(static (a, b) => ByteSequenceComparer.Instance.Compare(a.Url, b.Url));
        }

        return map;
    }

    /// <summary>Returns true when <paramref name="path"/> sits under <paramref name="directory"/>.</summary>
    /// <param name="path">Absolute candidate path.</param>
    /// <param name="directory">Absolute directory.</param>
    /// <returns>True for descendants; false otherwise.</returns>
    private static bool IsUnder(string path, DirectoryPath directory)
    {
        if (directory.IsEmpty)
        {
            return false;
        }

        var dirValue = directory.Value;
        var sep = Path.DirectorySeparatorChar;
        var altSep = Path.AltDirectorySeparatorChar;
        if (path.Length <= dirValue.Length)
        {
            return false;
        }

        if (!path.StartsWith(dirValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var next = path[dirValue.Length];
        return next == sep || next == altSep;
    }

    /// <summary>Translates a source-relative markdown path (e.g. <c>guide/intro.md</c>) to UTF-8 HTML URL bytes.</summary>
    /// <param name="markdownRelativePath">Source-relative path with platform separators.</param>
    /// <returns>UTF-8 forward-slashed bytes with the <c>.html</c> extension.</returns>
    private static byte[] ToHtmlUrlBytes(string markdownRelativePath)
    {
        var span = markdownRelativePath.AsSpan();
        var endsWithMd = span.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var keep = endsWithMd ? span.Length - MarkdownExtensionLength : span.Length;
        var totalLength = keep + (endsWithMd ? ".html".Length : 0);
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
            var lineEnd = YamlByteScanner.LineEnd(markdownBytes, cursor);
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
    private static void WriteIndexMarkdown(IBufferWriter<byte> writer, SortedDictionary<byte[], List<(byte[] Url, byte[] Title)>> grouped)
    {
        writer.Write("# Tags\n\n"u8);
        foreach (var pair in grouped)
        {
            writer.Write("- ["u8);
            writer.Write(pair.Key);
            writer.Write("]("u8);
            writer.Write(SlugifyTag(pair.Key));
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

    /// <summary>Builds a <c>{slug}.md</c> file name from ASCII slug bytes in a single allocation.</summary>
    /// <param name="slug">Slug bytes (ASCII alphanumeric / hyphen only, by construction of <see cref="SlugifyInto"/>).</param>
    /// <returns>The slug followed by the <c>.md</c> extension.</returns>
    private static string BuildSlugMarkdownFileName(byte[] slug) =>
        string.Create(slug.Length + MarkdownExtensionLength, slug, static (dst, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = (char)src[i];
            }

            ".md".AsSpan().CopyTo(dst[src.Length..]);
        });

    /// <summary>Lowercases <paramref name="tag"/> and replaces non-alphanumeric ASCII runs with single hyphens for use as a filename.</summary>
    /// <param name="tag">UTF-8 tag display bytes.</param>
    /// <returns>UTF-8 filesystem-safe slug bytes; <c>"tag"</c> when the input has no slug-safe bytes.</returns>
    private static byte[] SlugifyTag(ReadOnlySpan<byte> tag)
    {
        if (tag.IsEmpty)
        {
            return [.. "tag"u8];
        }

        Span<byte> stack = tag.Length <= 256 ? stackalloc byte[tag.Length] : new byte[tag.Length];
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
            if (b is >= (byte)'A' and <= (byte)'Z')
            {
                count = FlushHyphen(dst, count, pendingHyphen);
                dst[count++] = (byte)(b | AsciiCaseBit);
                pendingHyphen = false;
                continue;
            }

            if (b is >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9')
            {
                count = FlushHyphen(dst, count, pendingHyphen);
                dst[count++] = b;
                pendingHyphen = false;
                continue;
            }

            pendingHyphen = count is not 0;
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
