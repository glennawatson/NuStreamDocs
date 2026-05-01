// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Autorefs;

namespace NuStreamDocs.Xrefs.Tests;

/// <summary>Coverage for XrefsPlugin one-arg ctor and OnRenderPageAsync.</summary>
public class XrefsPluginCoverageTests
{
    /// <summary>One-arg ctor with a shared registry sets Registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RegistryCtor()
    {
        var registry = new AutorefsRegistry();
        var plugin = new XrefsPlugin(registry);
        await Assert.That(plugin.Registry).IsEqualTo(registry);
    }

    /// <summary>OnRenderPageAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRenderNoOp()
    {
        var plugin = new XrefsPlugin();
        await plugin.OnRenderPageAsync(new("p.md", default, new(8)), CancellationToken.None);
    }
}
