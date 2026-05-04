// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Branch-coverage tests for BlogPostScanner.Scan covering every filename and frontmatter shape.</summary>
public class BlogPostScannerBranchCoverageTests
{
    /// <summary>Missing posts directory returns an empty array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingDirectoryReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "smkd-bps-" + Guid.NewGuid().ToString("N"));
        await Assert.That(BlogPostScanner.Scan(path, path).Length).IsEqualTo(0);
    }

    /// <summary>Files without the YYYY-MM-DD prefix are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IgnoresNonDateFilenames()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "abc.md"), "no date");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "2026X01-02-foo.md"), "broken");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "2026-99-99-bad.md"), "invalid date");
        await Assert.That(BlogPostScanner.Scan(temp.Root, temp.Root).Length).IsEqualTo(0);
    }

    /// <summary>Posts with frontmatter title use it; posts without get humanized slug.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ScanProducesPosts()
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-01-15-hello-world.md"),
            "---\ntitle: Custom Title\nauthor: Alice\ntags: [a, b]\npublished: 2026-01-20\n---\n\nFirst para.\n\nSecond para.\n");
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "2026-02-10-no-frontmatter.md"),
            "Just a body, no frontmatter.\n");

        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts.Length).IsEqualTo(2);

        // Newest first: 2026-02-10 > 2026-01-20
        await Assert.That(posts[0].Slug.AsSpan().SequenceEqual("no-frontmatter"u8)).IsTrue();
        await Assert.That(posts[0].Title.AsSpan().SequenceEqual("No Frontmatter"u8)).IsTrue();
        await Assert.That(posts[1].Title.AsSpan().SequenceEqual("Custom Title"u8)).IsTrue();
        await Assert.That(posts[1].Author.AsSpan().SequenceEqual("Alice"u8)).IsTrue();
        await Assert.That(posts[1].Tags.Length).IsEqualTo(2);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-bps-" + Guid.NewGuid().ToString("N"));
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
