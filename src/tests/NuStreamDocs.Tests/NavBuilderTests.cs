// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Config;

namespace NuStreamDocs.Tests;

/// <summary>Tests for <c>NavBuilder</c>.</summary>
public class NavBuilderTests
{
    /// <summary>Zero count yields an empty array (no copy).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ZeroCountIsEmpty()
    {
        var buffer = new NavEntry[8];
        var result = NavBuilder.ToArray(buffer, 0);
        await Assert.That(result).IsEmpty();
    }

    /// <summary>The result is a freshly-sized array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResultIsRightSized()
    {
        var buffer = new NavEntry[8];
        buffer[0] = new("First", "first.md");
        buffer[1] = new("Second", "second.md");
        var result = NavBuilder.ToArray(buffer, 2);
        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo(buffer[0]);
        await Assert.That(result[1]).IsEqualTo(buffer[1]);
    }

    /// <summary>A null buffer is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullBufferRejected()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => NavBuilder.ToArray(null!, 0));
        await Assert.That(ex).IsNotNull();
    }
}
