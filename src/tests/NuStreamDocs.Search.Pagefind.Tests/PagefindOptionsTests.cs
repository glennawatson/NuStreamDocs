// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Defaults coverage for <see cref="PagefindOptions"/>.</summary>
public class PagefindOptionsTests
{
    /// <summary><see cref="PagefindOptions.Default"/> has the documented baseline values.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultHasExpectedValues()
    {
        var d = PagefindOptions.Default;
        await Assert.That(d.OutputSubdirectory.Value).IsEqualTo("search");
        await Assert.That(d.MinTokenLength).IsEqualTo(3);
        await Assert.That(d.SearchableFrontmatterKeys.Length).IsEqualTo(0);
        await Assert.That(d.SectionPriorities.Length).IsEqualTo(0);
        await Assert.That(d.RunCli).IsTrue();
        await Assert.That(d.BinaryPath.IsEmpty).IsTrue();
        await Assert.That(d.StrictBinaryRequired).IsFalse();
        await Assert.That(d.ExcludePathPrefixes.Length).IsEqualTo(0);
    }

    /// <summary><c>with</c>-expression edits leave the canonical <see cref="PagefindOptions.Default"/> untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultIsNotMutatedByWithExpression()
    {
        _ = PagefindOptions.Default with { MinTokenLength = 99 };
        await Assert.That(PagefindOptions.Default.MinTokenLength).IsEqualTo(3);
    }
}
