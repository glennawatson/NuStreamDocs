// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Coverage for BlogIndexEmitter.CreateArchiveWriter.</summary>
public class BlogIndexEmitterCoverageTests
{
    /// <summary>CreateArchiveWriter returns a fresh ArrayBufferWriter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ArchiveWriterFresh()
    {
        var writer = BlogIndexEmitter.CreateArchiveWriter();
        await Assert.That(writer).IsNotNull();
        await Assert.That(writer.WrittenCount).IsEqualTo(0);
    }
}
