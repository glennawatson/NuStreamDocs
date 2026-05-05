// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Sitemap.Tests;

/// <summary>Lifecycle method coverage for NotFoundPlugin.</summary>
public class NotFoundPluginLifecycleTests
{
    /// <summary>Name returns "404".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        NotFoundPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("404"u8)).IsTrue();
    }
}
