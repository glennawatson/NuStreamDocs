// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the <see cref="ByteRange"/> struct.</summary>
public class ByteRangeTests
{
    /// <summary>IsEmpty is true when Length is 0.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IsEmpty_reflects_length()
    {
        await Assert.That(new ByteRange(0, 0).IsEmpty).IsTrue();
        await Assert.That(new ByteRange(10, 0).IsEmpty).IsTrue();
        await Assert.That(new ByteRange(0, 5).IsEmpty).IsFalse();
    }
}
