// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Parameterised frontmatter combinations for BlogPostScanner.</summary>
public class BlogPostScannerFrontmatterTests
{
    /// <summary>Each title shape (inline, quoted, with-symbols) flows through to BlogPost.Title.</summary>
    /// <param name="titleField">Frontmatter title line.</param>
    /// <param name="expected">Expected BlogPost.Title.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("title: Hello", "Hello")]
    [Arguments("title: With Spaces And Words", "With Spaces And Words")]
    [Arguments("title: With-symbol!", "With-symbol!")]
    public async Task TitleShapes(string titleField, string expected)
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-01-slug.md"),
            $"---\n{titleField}\n---\nbody\n");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts[0].Title).IsEqualTo(expected);
    }

    /// <summary>Author field shapes round-trip through BlogPost.Author.</summary>
    /// <param name="authorField">Frontmatter author line.</param>
    /// <param name="expected">Expected BlogPost.Author.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("author: Alice", "Alice")]
    [Arguments("author: Alice Bob", "Alice Bob")]
    [Arguments("author: A.B.", "A.B.")]
    public async Task AuthorShapes(string authorField, string expected)
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-02-slug.md"),
            $"---\n{authorField}\n---\nbody\n");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts[0].Author).IsEqualTo(expected);
    }

    /// <summary>Tags list shapes parse into Tags array.</summary>
    /// <param name="tagsField">Frontmatter tags definition.</param>
    /// <param name="expectedCount">Expected tag count.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("tags: [a]", 1)]
    [Arguments("tags: [a, b]", 2)]
    [Arguments("tags: [a, b, c]", 3)]
    public async Task TagsListShapes(string tagsField, int expectedCount)
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-03-slug.md"),
            $"---\n{tagsField}\n---\nbody\n");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts[0].Tags.Length).IsEqualTo(expectedCount);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-bpf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch root.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
