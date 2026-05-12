// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <c>DocBuilderFontsExtensions</c> overloads.</summary>
public class DocBuilderFontsExtensionsTests
{
    /// <summary>The options overload returns a non-null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsOverload()
    {
        var b = new DocBuilder().UseFonts(FontsOptions.Default.AddGoogleFont("Inter"u8));
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The configure-delegate overload runs the delegate against the defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureDelegateOverload()
    {
        var b = new DocBuilder().UseFonts(static o => o.AddGoogleFont("Source Sans 3"u8, 400, 700));
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The options + logger overload registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsLoggerOverload()
    {
        var b = new DocBuilder().UseFonts(FontsOptions.Default, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }
}
