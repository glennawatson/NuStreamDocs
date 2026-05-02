// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common.Logging;

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
        BlogLoggingHelper.LogDiscoveryStart(logger, options.PostsRoot);
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

        Directory.CreateDirectory(Path.GetDirectoryName(options.IndexPath)!);
        var writer = BlogIndexEmitter.CreateIndexWriter();
        BlogIndexEmitter.WriteIndex(writer, options.IndexTitle, posts);
        await File.WriteAllBytesAsync(options.IndexPath, writer.WrittenMemory, cancellationToken).ConfigureAwait(false);

        if (!options.EmitArchives)
        {
            BlogLoggingHelper.LogIndexGenerated(logger, options.IndexPath, 0);
            return;
        }

        Directory.CreateDirectory(options.ArchiveRoot);
        var archiveCount = 0;
        foreach (var (tag, postsForTag) in GroupByTag(posts))
        {
            var safeSlug = BlogSlugifier.Slugify(tag, options.ArchiveFallbackSlug);
            var archivePath = Path.Combine(options.ArchiveRoot, safeSlug + ".md");
            writer.ResetWrittenCount();
            BlogIndexEmitter.WriteTagArchive(writer, tag, [.. postsForTag]);
            await File.WriteAllBytesAsync(archivePath, writer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            archiveCount++;
        }

        BlogLoggingHelper.LogIndexGenerated(logger, options.IndexPath, archiveCount);
    }

    /// <summary>Buckets posts by tag with deterministic ordering.</summary>
    /// <param name="posts">Posts to group.</param>
    /// <returns>Tag → posts map.</returns>
    private static SortedDictionary<string, List<BlogPost>> GroupByTag(BlogPost[] posts)
    {
        var map = new SortedDictionary<string, List<BlogPost>>(StringComparer.Ordinal);
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
