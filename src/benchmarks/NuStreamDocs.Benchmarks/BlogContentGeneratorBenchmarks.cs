// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Blog.Common;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Baseline + post-migration benchmarks for <see cref="BlogContentGenerator"/>.
/// Captures the per-build cost of scanning the posts root, building the index +
/// per-tag archives, and emitting them — currently to disk, but the post-migration
/// path will route through <c>SyntheticPageSink</c> instead. Run before and after
/// the migration with the same parameter set so the wall-time / allocation deltas
/// are directly comparable.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
[SuppressMessage(
    "Major Code Smell",
    "S4462:Calls to \"async\" methods should not be blocking",
    Justification = "BenchmarkDotNet drives benchmarks synchronously; GetResult is the pragmatic way to measure end-to-end async pipelines.")]
public class BlogContentGeneratorBenchmarks
{
    /// <summary>Small post count (smoke).</summary>
    private const int SmallPosts = 10;

    /// <summary>Medium post count (typical project; rxui currently sits around 120).</summary>
    private const int MediumPosts = 100;

    /// <summary>Large post count (stress).</summary>
    private const int LargePosts = 500;

    /// <summary>ISO-8601 date format used for synthetic post filenames + frontmatter.</summary>
    private const string IsoDate = "yyyy-MM-dd";

    /// <summary>Stable epoch the synthetic posts step from; <see cref="DateOnly"/> because posts only carry a date.</summary>
    private static readonly DateOnly PostEpoch = new(2024, 1, 1);

    /// <summary>Distinct tags spread across posts so the per-tag archive emitter actually exercises grouping.</summary>
    private static readonly string[] Tags =
    [
        "Article",
        "Release Notes",
        "Tutorial",
        "Announcement",
        "Deep Dive",
    ];

    /// <summary>Absolute path to the synthetic docs root; the posts root is the <c>articles</c> subdirectory and the archive root is <c>articles/tags</c>.</summary>
    private string _docsRoot = string.Empty;

    /// <summary>Pre-built generation options reused by every iteration.</summary>
    private BlogGenerationOptions _options = null!;

    /// <summary>Gets or sets the post count for the current parameter set.</summary>
    [Params(SmallPosts, MediumPosts, LargePosts)]
    public int Posts { get; set; }

    /// <summary>Generates the synthetic posts corpus once per parameter set.</summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        _docsRoot = Path.Combine(Path.GetTempPath(), "smkd-bench-blog-docs-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var postsRoot = Path.Combine(_docsRoot, "articles");
        var archiveRoot = Path.Combine(postsRoot, "tags");
        Directory.CreateDirectory(postsRoot);

        for (var i = 0; i < Posts; i++)
        {
            var tag = Tags[i % Tags.Length];
            var path = Path.Combine(postsRoot, BuildPostFileName(i));
            File.WriteAllText(path, BuildPostBody(i, tag));
        }

        _options = new BlogGenerationOptions(
            PostsRoot: (DirectoryPath)postsRoot,
            DocsRoot: (DirectoryPath)_docsRoot,
            IndexPath: ((DirectoryPath)postsRoot).File("index.md"),
            IndexTitle: "Articles"u8.ToArray(),
            EmitArchives: true,
            ArchiveRoot: (DirectoryPath)archiveRoot,
            ArchiveFallbackSlug: "tag"u8.ToArray());
    }

    /// <summary>Cleans the corpus once at the end.</summary>
    [GlobalCleanup]
    public void GlobalCleanup() => TryDelete(_docsRoot);

    /// <summary>End-to-end <see cref="BlogContentGenerator.GenerateAsync"/> on the configured corpus, registering pages with a fresh per-iteration <see cref="SyntheticPageSink"/>.</summary>
    /// <returns>Number of synthetic pages registered (returned so BenchmarkDotNet doesn't elide the call).</returns>
    [Benchmark]
    public int Generate()
    {
        SyntheticPageSink sink = new();
        BlogContentGenerator.GenerateAsync(NullLogger.Instance, _options, sink, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        return sink.Count;
    }

    /// <summary>Builds a deterministic per-post filename of the form <c>YYYY-MM-DD-post-N.md</c>.</summary>
    /// <param name="index">Zero-based post index.</param>
    /// <returns>Slug + extension.</returns>
    [SuppressMessage("Major Code Smell", "S6585:Do not hardcode the format specifier", Justification = "ISO-8601 date is the canonical Wyam blog frontmatter shape; matching it deliberately.")]
    private static string BuildPostFileName(int index) =>
        string.Concat(
            PostEpoch.AddDays(index).ToString(IsoDate, CultureInfo.InvariantCulture),
            "-post-",
            index.ToString(CultureInfo.InvariantCulture),
            ".md");

    /// <summary>Builds a realistic-shape post body with frontmatter (title/tag/date) and a few paragraphs of prose.</summary>
    /// <param name="index">Post index.</param>
    /// <param name="tag">Tag the post belongs to.</param>
    /// <returns>Markdown source.</returns>
    [SuppressMessage("Major Code Smell", "S6585:Do not hardcode the format specifier", Justification = "ISO-8601 date is the canonical Wyam blog frontmatter shape; matching it deliberately.")]
    private static string BuildPostBody(int index, string tag) =>
        new StringBuilder(1024)
            .Append("---\n")
            .Append("Title: Post ").Append(index.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("Tags: ").Append(tag).Append('\n')
            .Append("Author: Bench Author\n")
            .Append("Published: ").Append(PostEpoch.AddDays(index).ToString(IsoDate, CultureInfo.InvariantCulture)).Append('\n')
            .Append("---\n\n")
            .Append("First paragraph for post ").Append(index.ToString(CultureInfo.InvariantCulture)).Append(" used as the index excerpt.\n\n")
            .Append("Second paragraph with a `code` reference and a [link](https://example.com).\n\n")
            .Append("Third paragraph with more prose to flesh out the body.\n")
            .ToString();

    /// <summary>Best-effort recursive directory delete.</summary>
    /// <param name="path">Directory path.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
