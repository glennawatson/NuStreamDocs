// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Coverage for AutorefsRegistry.Clear.</summary>
public class AutorefsRegistryClearTests
{
    /// <summary>Clear empties the registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearEmpties()
    {
        var registry = new AutorefsRegistry();
        registry.Register("Foo"u8, "/x.html"u8.ToArray(), fragment: default);
        registry.Clear();
        await Assert.That(registry.Count).IsEqualTo(0);
        await Assert.That(registry.TryResolve("Foo"u8, out _)).IsFalse();
    }
}
