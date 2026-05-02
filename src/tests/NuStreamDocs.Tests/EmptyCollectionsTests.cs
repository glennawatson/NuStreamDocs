// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the <see cref="EmptyCollections"/> helper.</summary>
public class EmptyCollectionsTests
{
    /// <summary>HashSetFor returns an empty HashSet with the right comparer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HashSetFor_returns_empty()
    {
        var set = EmptyCollections.HashSetFor<string>();
        await Assert.That(set.Count).IsEqualTo(0);
    }
}
