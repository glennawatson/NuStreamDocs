// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Blog.Common.Logging;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Shared blog index/archive generator used by the built-in blog plugins.
/// Pages flow through a <see cref="SyntheticPageSink"/> rather than landing
/// on disk, so the source folder stays clean and the rendered output appears
/// directly under <c>site/</c> via the regular render pipeline.
/// </summary>
public static class BlogContentGenerator
{
    /// <summary>Initial bucket capacity for the per-tag post list.</summary>
    private const int TagBucketCapacity = 8;

    /// <summary>Initial pool rental size for the shared page writer; matches the index-page hint inside <see cref="BlogIndexEmitter"/>.</summary>
    private const int InitialPageWriterCapacity = 2 * 1024;

    /// <summary>Scans posts and registers the generated index + archive pages with <paramref name="sink"/>.</summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="sink">Destination synthetic-page sink the rendered pages are added to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once every page has been registered with the sink.</returns>
    public static ValueTask GenerateAsync(
        ILogger logger,
        in BlogGenerationOptions options,
        SyntheticPageSink sink,
        in CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sink);
        options.Validate();
        return GenerateCoreAsync(logger, options, sink, cancellationToken);
    }

    /// <summary>Scans posts and registers the generated index + archive pages.</summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Generation options.</param>
    /// <param name="sink">Destination synthetic-page sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A task that completes once registration is done; the await is currently synchronous
    /// but the API stays async-shaped to allow future I/O-bound enrichments without
    /// breaking callers.
    /// </returns>
    private static async ValueTask GenerateCoreAsync(
        ILogger logger,
        BlogGenerationOptions options,
        SyntheticPageSink sink,
        CancellationToken cancellationToken)
    {
        // Fully synchronous today, but the entry point keeps its async signature so
        // future I/O-bound enrichments (e.g. remote feed merging) don't break callers.
        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

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
        var indexDirectoryRelativeUtf8 = ComputeRelativeDirectoryUtf8(options.DocsRoot, indexDirectory);

        // Pool-backed writer rented once and reused across every page emit. The
        // PageBuilderPool's thread-static caching means the underlying ArrayBufferWriter
        // is shared with every other plugin that uses Rent — and the cross-build warmup
        // means the first build pays the buffer allocation, subsequent builds reuse.
        // Per-page detach hands each SyntheticPage its own owned bytes via
        // WrittenSpan.ToArray, and ResetWrittenCount between pages keeps the rental
        // active for the next emit.
        using var rental = PageBuilderPool.Rent(InitialPageWriterCapacity);
        var writer = rental.Writer;
        BlogIndexEmitter.WriteIndex(writer, options.IndexTitle, posts, indexDirectoryRelativeUtf8);
        sink.Add(new(options.DocsRoot.Relative(options.IndexPath), writer.WrittenSpan.ToArray()));

        var pagesBytes = BlogPagesFileEmitter.Render(posts);
        sink.Add(new(options.DocsRoot.Relative(indexDirectory.File(".pages")), pagesBytes));

        if (!options.EmitArchives)
        {
            BlogLoggingHelper.LogIndexGenerated(logger, options.IndexPath.Value, 0);
            return;
        }

        var archiveDirectoryRelativeUtf8 = ComputeRelativeDirectoryUtf8(options.DocsRoot, options.ArchiveRoot);
        var archiveCount = 0;
        var fallback = options.ArchiveFallbackSlug;
        foreach (var (tag, postsForTag) in GroupByTag(posts))
        {
            var safeSlugBytes = BlogSlugifier.Slugify(tag, fallback);
            var archiveFile = options.ArchiveRoot.File(Encoding.UTF8.GetString(safeSlugBytes) + ".md");
            writer.ResetWrittenCount();
            BlogIndexEmitter.WriteTagArchive(writer, tag, [.. postsForTag], archiveDirectoryRelativeUtf8);
            sink.Add(new(options.DocsRoot.Relative(archiveFile), writer.WrittenSpan.ToArray()));
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
