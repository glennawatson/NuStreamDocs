// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>PathFilter</c>.</summary>
public class PathFilterTests
{
    /// <summary>The empty filter keeps everything and reports no rules.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyFilterKeepsEverything()
    {
        await Assert.That(PathFilter.Empty.HasRules).IsFalse();
        await Assert.That(PathFilter.Empty.Matches("anything/at/all.md")).IsTrue();
    }

    /// <summary>Excludes drop matching paths and keep the rest.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExcludeDropsMatches()
    {
        PathFilter filter = new([], ["drafts/**", "**/_partial.md"]);

        await Assert.That(filter.HasRules).IsTrue();
        await Assert.That(filter.Matches("guide/intro.md")).IsTrue();
        await Assert.That(filter.Matches("drafts/wip.md")).IsFalse();
        await Assert.That(filter.Matches("guide/_partial.md")).IsFalse();
    }

    /// <summary>When includes are configured, only matches survive.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IncludeRestrictsToMatches()
    {
        PathFilter filter = new(["guide/**/*.md"], []);

        await Assert.That(filter.Matches("guide/intro.md")).IsTrue();
        await Assert.That(filter.Matches("guide/sub/page.md")).IsTrue();
        await Assert.That(filter.Matches("blog/post.md")).IsFalse();
    }

    /// <summary>Excludes win over includes when both match.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExcludeBeatsInclude()
    {
        PathFilter filter = new(["guide/**/*.md"], ["**/_partial.md"]);

        await Assert.That(filter.Matches("guide/intro.md")).IsTrue();
        await Assert.That(filter.Matches("guide/_partial.md")).IsFalse();
    }
}
