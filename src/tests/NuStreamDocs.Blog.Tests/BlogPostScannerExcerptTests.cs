// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Branch-coverage tests for BlogPostScanner excerpt + humanise paths.</summary>
public class BlogPostScannerExcerptTests
{
    /// <summary>Post with no body returns an empty excerpt.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyBodyHasNoExcerpt()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-01-empty.md"),
            "---\ntitle: Empty\n---\n");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts.Length).IsEqualTo(1);
        await Assert.That(posts[0].Excerpt).IsEqualTo(string.Empty);
    }

    /// <summary>Post body with leading ATX heading skips it for the excerpt.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AtxHeadingSkippedInExcerpt()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-02-heading.md"),
            "# Title\n\nFirst paragraph wins.\n");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts.Length).IsEqualTo(1);
        await Assert.That(posts[0].Excerpt).IsEqualTo("First paragraph wins.");
    }

    /// <summary>Post body with only blank lines yields an empty excerpt.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnlyBlankLines()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-03-blank.md"),
            "---\ntitle: Blank\n---\n\n\n\n");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts[0].Excerpt).IsEqualTo(string.Empty);
    }

    /// <summary>Post body without a trailing newline is read whole.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoTrailingNewline()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-04-noeol.md"),
            "Inline post with no trailing newline");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts[0].Excerpt).IsEqualTo("Inline post with no trailing newline");
    }

    /// <summary>Multi-word slug humanises to title case with spaces.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HumaniseMultipleHyphens()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-05-my-cool-post-name.md"),
            "Body");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts[0].Title).IsEqualTo("My Cool Post Name");
    }

    /// <summary>Slug with consecutive hyphens collapses spaces.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HumaniseConsecutiveHyphens()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-04-06-double--hyphen.md"),
            "Body");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts[0].Title).IsEqualTo("Double Hyphen");
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-bpsx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch directory.</summary>
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
