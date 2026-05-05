// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Blog.Tests;

/// <summary>Lifecycle method coverage for <c>WyamBlogPlugin</c>.</summary>
public class WyamBlogPluginLifecycleTests
{
    /// <summary>DiscoverAsync runs without error against an empty posts directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DiscoverAsync()
    {
        using ScratchDir temp = new();
        Directory.CreateDirectory(Path.Combine(temp.Root, "posts"));
        WyamBlogPlugin plugin = new(new("posts", [.. "Blog"u8]));
        await plugin.DiscoverAsync(new(temp.Root, "/out", []), CancellationToken.None);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-wb-" + Guid.NewGuid().ToString("N"));
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
