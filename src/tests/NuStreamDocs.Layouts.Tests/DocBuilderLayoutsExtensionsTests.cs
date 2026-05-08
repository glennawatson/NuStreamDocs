// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Layouts.Tests;

/// <summary>Tests for the <see cref="DocBuilderLayoutsExtensions"/> helpers.</summary>
public class DocBuilderLayoutsExtensionsTests
{
    /// <summary>UseLayouts() registers the plugin with default options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLayouts_default_registers_plugin()
    {
        DocBuilder builder = new();
        var result = builder.UseLayouts();
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>UseLayouts(configure) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLayouts_with_configure_registers_plugin()
    {
        DocBuilder builder = new();
        var result = builder.UseLayouts(static opt => opt);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>UseLayouts(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLayouts_with_options_registers_plugin()
    {
        DocBuilder builder = new();
        var result = builder.UseLayouts(LayoutsOptions.Default);
        await Assert.That(result).IsSameReferenceAs(builder);
    }
}
