// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins.ExtraAssets;

namespace NuStreamDocs.Tests;

/// <summary>Coverage for ExtraAssetsPlugin Name + OnRenderPageAsync.</summary>
public class ExtraAssetsPluginCoverageTests
{
    /// <summary>Name returns "extra-assets".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        var plugin = new ExtraAssetsPlugin();
        await Assert.That(plugin.Name.AsSpan().SequenceEqual("extra-assets"u8)).IsTrue();
    }

    /// <summary>OnRenderPageAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderNoOp()
    {
        var plugin = new ExtraAssetsPlugin();
        await plugin.OnRenderPageAsync(new("p.md", default, new(8)), CancellationToken.None);
    }
}
