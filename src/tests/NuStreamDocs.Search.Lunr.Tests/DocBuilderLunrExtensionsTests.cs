// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Lunr.Tests;

/// <summary>Coverage for <c>DocBuilderLunrExtensions</c> overloads.</summary>
public class DocBuilderLunrExtensionsTests
{
    /// <summary>Default-args overload returns a non-null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsOverload()
    {
        var b = new DocBuilder().UseLunrSearch();
        await Assert.That(b).IsNotNull();
    }

    /// <summary>Configure-delegate overload runs the delegate against the defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureDelegateOverload()
    {
        var b = new DocBuilder().UseLunrSearch(static o => o.WithLanguage("fr"));
        await Assert.That(b).IsNotNull();
    }

    /// <summary>Options + logger overload registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsLoggerOverload()
    {
        var options = LunrOptions.Default;
        var b = new DocBuilder().UseLunrSearch(options, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }
}
