// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Versions.Tests;

/// <summary>Lifecycle method coverage for VersionsPlugin.</summary>
public class VersionsPluginLifecycleTests
{
    /// <summary>Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        var plugin = new VersionsPlugin(new("1.0", "Stable"));
        await Assert.That(plugin.Name.SequenceEqual("versions"u8)).IsTrue();
    }
}
