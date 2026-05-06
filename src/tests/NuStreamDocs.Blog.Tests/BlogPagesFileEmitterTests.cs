// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Tests for the literate-nav <c>.pages</c> emitter.</summary>
public class BlogPagesFileEmitterTests
{
    /// <summary>Three dated posts emit a <c>nav:</c> list with index first and posts in newest-first order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RendersIndexFirstThenPostsNewestToOldest()
    {
        using ScratchDir temp = new();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "2024-01-01-alpha.md"), "Body");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "2025-01-01-bravo.md"), "Body");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "2026-01-01-charlie.md"), "Body");

        var posts = BlogPostScanner.Scan(temp.Root, temp.Root);
        var bytes = BlogPagesFileEmitter.Render(posts);
        var rendered = Encoding.UTF8.GetString(bytes);

        const string expected = "nav:\n  - index.md\n  - 2026-01-01-charlie.md\n  - 2025-01-01-bravo.md\n  - 2024-01-01-alpha.md\n";
        await Assert.That(rendered).IsEqualTo(expected);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-bpfe-" + Guid.NewGuid().ToString("N"));
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
