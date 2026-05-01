// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Parameterised filename + frontmatter shape tests for BlogPostScanner.</summary>
public class BlogPostScannerParameterisedTests
{
    /// <summary>Files without the YYYY-MM-DD prefix or with malformed dates are skipped.</summary>
    /// <param name="filename">File name beneath the posts root.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("abc.md")]
    [Arguments("2026-01-02.md")]
    [Arguments("202601-02-foo.md")]
    [Arguments("2026X01-02-foo.md")]
    [Arguments("2026-01X02-foo.md")]
    [Arguments("2026-01-02xfoo.md")]
    [Arguments("2026-13-01-foo.md")]
    [Arguments("2026-02-30-foo.md")]
    public async Task RejectedFilenames(string filename)
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, filename), "body");
        await Assert.That(BlogPostScanner.Scan(temp.Root, temp.Root).Length).IsEqualTo(0);
    }

    /// <summary>Slug variants humanise into title-cased space-separated names.</summary>
    /// <param name="slug">Slug part of the filename.</param>
    /// <param name="expected">Expected humanised title.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("hello", "Hello")]
    [Arguments("hello-world", "Hello World")]
    [Arguments("a-b-c-d", "A B C D")]
    [Arguments("trailing-", "Trailing")]
    [Arguments("-leading", "Leading")]
    [Arguments("double--hyphen", "Double Hyphen")]
    public async Task HumanisedSlugVariants(string slug, string expected)
    {
        using var temp = new ScratchDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, $"2026-01-15-{slug}.md"), "body");
        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        await Assert.That(posts.Length).IsEqualTo(1);
        await Assert.That(posts[0].Title).IsEqualTo(expected);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-bpsp-" + Guid.NewGuid().ToString("N"));
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
