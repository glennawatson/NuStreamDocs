// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog.MkDocs.Tests;

/// <summary>End-to-end tests for <c>MkDocsBlogPlugin</c>.</summary>
public class MkDocsBlogPluginTests
{
    /// <summary>The mkdocs variant pulls posts from <c>{blog}/posts/</c> and emits archives under <c>{blog}/category/</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UsesPostsAndCategoryDirectories()
    {
        var docsRoot = Path.Combine(Path.GetTempPath(), "smd-mkblog-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var blogRoot = Path.Combine(docsRoot, "blog");
        var postsRoot = Path.Combine(blogRoot, "posts");
        Directory.CreateDirectory(postsRoot);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(postsRoot, "2024-01-15-launch.md"), "---\nTitle: Launch\nAuthor: Team\nTags: Release\nPublished: 2024-01-15\n---\nLaunch announcement.");

            var plugin = new MkDocsBlogPlugin(new("blog", [.. "Blog"u8]));
            var ctx = new BuildDiscoverContext(docsRoot, "/out", []);
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            var index = await File.ReadAllTextAsync(Path.Combine(blogRoot, "index.md"));
            await Assert.That(index.Contains("Launch", StringComparison.Ordinal)).IsTrue();

            var archive = await File.ReadAllTextAsync(Path.Combine(blogRoot, "category", "release.md"));
            await Assert.That(archive.Contains("Launch", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            Directory.Delete(docsRoot, recursive: true);
        }
    }
}
