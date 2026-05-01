// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests that <c>PageDiscovery</c> honours <c>PathFilter</c>.</summary>
public class PageDiscoveryFilterTests
{
    /// <summary>Excluded paths never appear in the discovered stream.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiscoveryHonoursExclude()
    {
        var root = Path.Combine(Path.GetTempPath(), "smd-discovery-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "drafts"));
            Directory.CreateDirectory(Path.Combine(root, "guide"));
            await File.WriteAllTextAsync(Path.Combine(root, "guide", "intro.md"), "# Intro");
            await File.WriteAllTextAsync(Path.Combine(root, "drafts", "wip.md"), "# WIP");

            var filter = new PathFilter([], ["drafts/**"]);
            var hits = new List<string>();
            await foreach (var item in PageDiscovery.EnumerateAsync(root, filter, CancellationToken.None))
            {
                hits.Add(item.RelativePath);
            }

            await Assert.That(hits).Contains("guide/intro.md");
            await Assert.That(hits.Contains("drafts/wip.md")).IsFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Includes restrict the discovered stream to matching paths only.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiscoveryHonoursInclude()
    {
        var root = Path.Combine(Path.GetTempPath(), "smd-discovery-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "guide"));
            Directory.CreateDirectory(Path.Combine(root, "blog"));
            await File.WriteAllTextAsync(Path.Combine(root, "guide", "intro.md"), "# Intro");
            await File.WriteAllTextAsync(Path.Combine(root, "blog", "post.md"), "# Post");

            var filter = new PathFilter(["guide/**/*.md"], []);
            var hits = new List<string>();
            await foreach (var item in PageDiscovery.EnumerateAsync(root, filter, CancellationToken.None))
            {
                hits.Add(item.RelativePath);
            }

            await Assert.That(hits).Contains("guide/intro.md");
            await Assert.That(hits.Contains("blog/post.md")).IsFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
