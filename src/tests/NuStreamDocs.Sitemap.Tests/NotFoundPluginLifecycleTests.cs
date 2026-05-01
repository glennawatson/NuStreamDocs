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
        var plugin = new NotFoundPlugin();
        await Assert.That(plugin.Name).IsEqualTo("404");
    }

    /// <summary>OnConfigureAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnConfigure() =>
        await new NotFoundPlugin().OnConfigureAsync(new(default, "/in", "/out", []), CancellationToken.None);

    /// <summary>OnRenderPageAsync no-ops.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnRender() =>
        await new NotFoundPlugin().OnRenderPageAsync(new("p.md", default, new(8)), CancellationToken.None);
}
