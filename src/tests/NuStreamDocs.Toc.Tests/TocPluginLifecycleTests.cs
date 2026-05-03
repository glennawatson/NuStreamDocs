// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc.Tests;

/// <summary>Lifecycle method coverage for TocPlugin.</summary>
public class TocPluginLifecycleTests
{
    /// <summary>Name and TocMarker accessors return their constants.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAndMarker()
    {
        var plugin = new TocPlugin();
        await Assert.That(plugin.Name).IsEqualTo("toc");
        await Assert.That(TocPlugin.TocMarker).IsEqualTo("<!--@@toc@@-->");
    }

    /// <summary>OnConfigureAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnConfigure() =>
        await new TocPlugin().OnConfigureAsync(new("/in", "/out", []), CancellationToken.None);

    /// <summary>OnFinalizeAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinalize() =>
        await new TocPlugin().OnFinalizeAsync(new("/out"), CancellationToken.None);
}
