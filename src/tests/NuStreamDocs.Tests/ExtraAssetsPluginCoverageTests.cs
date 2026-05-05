// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Tests;

/// <summary>Coverage for the ExtraAssetsPlugin name accessor.</summary>
public class ExtraAssetsPluginCoverageTests
{
    /// <summary>Name returns "extra-assets".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        ExtraAssetsPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("extra-assets"u8)).IsTrue();
    }
}
