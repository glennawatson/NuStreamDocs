// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Macros.Tests;

/// <summary>Tests for the <see cref="DocBuilderMacrosExtensions"/> helpers.</summary>
public class DocBuilderMacrosExtensionsTests
{
    /// <summary>UseMacros(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMacros_with_options_registers_plugin()
    {
        var builder = new DocBuilder();
        var result = builder.UseMacros(MacrosOptions.Default);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>UseMacros(configure) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMacros_with_configure_registers_plugin()
    {
        var builder = new DocBuilder();
        var result = builder.UseMacros(opt => opt);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>UseMacros() registers the plugin with default options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMacros_default_registers_plugin()
    {
        var builder = new DocBuilder();
        var result = builder.UseMacros();
        await Assert.That(result).IsSameReferenceAs(builder);
    }
}
