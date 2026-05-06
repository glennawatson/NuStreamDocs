// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Blog.Common.Logging;
using NuStreamDocs.Common;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Shared blog index/archive generator used by the built-in blog plugins.
/// </summary>
public static class BlogContentGenerator
{
    /// <summary>Initial bucket capacity for the per-tag post list.</summary>
    private const int TagBucketCapacity = 8;

    /// <summary>Scans posts and writes the generated index and archive pages.</summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the generated files are on disk.</returns>
    public static ValueTask GenerateAsync(ILogger logger, in BlogGenerationOptions options, in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return GenerateCoreAsync(logger, options, cancellationToken);
    }

    /// <summary>Scans posts and writes the generated index and archive pages.</summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the generated files are on disk.</returns>
    private static async ValueTask GenerateCoreAsync(ILogger logger, BlogGenerationOptions options, CancellationToken cancellationToken)
    {
        BlogLoggingHelper.LogDiscoveryStart(logger, options.PostsRoot.Value);
        var posts = BlogPostScanner.Scan(options.PostsRoot, options.DocsRoot);
        BlogLoggingHelper.LogDiscoveryComplete(logger, posts.Length, 0);
        if (logger.IsEnabled(LogLevel.Debug))
        {
            for (var i = 0; i < posts.Length; i++)
            {
                var post = posts[i];
                BlogLoggingHelper.LogPostDiscovered(logger, post.Slug, post.Published, post.Title);
            }
        }

        if (posts.Length is 0)
        {
            return;
        }

        var indexDirectory = options.IndexPath.Directory;
        Directory.CreateDirectory(indexDirectory);
        var indexDirectoryRelativeUtf8 = ComputeRelativeDirectoryUtf8(options.DocsRoot, indexDirectory);
        var writer = BlogIndexEmitter.CreateIndexWriter();
        BlogIndexEmitter.WriteIndex(writer, options.IndexTitle, posts, indexDirectoryRelativeUtf8);
        await File.WriteAllBytesAsync(options.IndexPath, writer.WrittenMemory, cancellationToken).ConfigureAwait(false);

        var pagesFilePath = Path.Combine(indexDirectory.Value, ".pages");
        var pagesBytes = BlogPagesFileEmitter.Render(posts);
        await File.WriteAllBytesAsync(pagesFilePath, pagesBytes, cancellationToken).ConfigureAwait(false);

        if (!options.EmitArchives)
        {
            BlogLoggingHelper.LogIndexGenerated(logger, options.IndexPath.Value, 0);
            return;
        }

        Directory.CreateDirectory(options.ArchiveRoot);
        var archiveDirectoryRelativeUtf8 = ComputeRelativeDirectoryUtf8(options.DocsRoot, options.ArchiveRoot);
        var archiveCount = 0;
        var fallback = options.ArchiveFallbackSlug;
        foreach (var (tag, postsForTag) in GroupByTag(posts))
        {
            var safeSlugBytes = BlogSlugifier.Slugify(tag, fallback);
            var archivePath = Path.Combine(options.ArchiveRoot, Encoding.UTF8.GetString(safeSlugBytes) + ".md");
            writer.ResetWrittenCount();
            BlogIndexEmitter.WriteTagArchive(writer, tag, [.. postsForTag], archiveDirectoryRelativeUtf8);
            await File.WriteAllBytesAsync(archivePath, writer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            archiveCount++;
        }

        BlogLoggingHelper.LogIndexGenerated(logger, options.IndexPath.Value, archiveCount);
    }

    /// <summary>Computes the relative directory path from <paramref name="docsRoot"/> to <paramref name="absoluteDirectory"/>, if any.</summary>
    /// <param name="docsRoot">Absolute docs root.</param>
    /// <param name="absoluteDirectory">Absolute directory path.</param>
    /// <returns>Forward-slashed UTF-8 bytes; empty when no relative segments are needed.</returns>
    private static byte[] ComputeRelativeDirectoryUtf8(DirectoryPath docsRoot, DirectoryPath absoluteDirectory)
    {
        if (absoluteDirectory.IsEmpty)
        {
            return [];
        }

        // BCL boundary: Path.GetRelativePath returns a string; the result is encoded to UTF-8 immediately.
        var relative = Path.GetRelativePath(docsRoot.Value, absoluteDirectory.Value);
        if (string.IsNullOrEmpty(relative) || relative is ".")
        {
            return [];
        }

        var dst = new byte[Encoding.UTF8.GetByteCount(relative)];
        var written = Encoding.UTF8.GetBytes(relative, dst);
        for (var i = 0; i < written; i++)
        {
            if (dst[i] is (byte)'\\')
            {
                dst[i] = (byte)'/';
            }
        }

        return dst;
    }

    /// <summary>Buckets posts by tag with deterministic ordering.</summary>
    /// <param name="posts">Posts to group.</param>
    /// <returns>Tag → posts map.</returns>
    private static SortedDictionary<byte[], List<BlogPost>> GroupByTag(BlogPost[] posts)
    {
        SortedDictionary<byte[], List<BlogPost>> map = new(ByteArrayComparer.Instance);
        for (var i = 0; i < posts.Length; i++)
        {
            var post = posts[i];
            for (var t = 0; t < post.Tags.Length; t++)
            {
                var tag = post.Tags[t];
                if (!map.TryGetValue(tag, out var bucket))
                {
                    bucket = new(TagBucketCapacity);
                    map[tag] = bucket;
                }

                bucket.Add(post);
            }
        }

        return map;
    }
}
