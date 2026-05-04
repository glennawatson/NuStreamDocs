// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Tests;

/// <summary>Direct tests for PagefindFallbackSlug.For.</summary>
public class PagefindIndexWriterFallbackTests
{
    /// <summary>Fallback returns "page-N" for any ordinal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FallbackShape()
    {
        await Assert.That(PagefindFallbackSlug.For(0).AsSpan().SequenceEqual("page-0"u8)).IsTrue();
        await Assert.That(PagefindFallbackSlug.For(42).AsSpan().SequenceEqual("page-42"u8)).IsTrue();
    }
}
