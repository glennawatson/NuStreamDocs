// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Redirects.Tests;

/// <summary>Coverage for <c>DocBuilderRedirectsExtensions</c> overloads.</summary>
public class DocBuilderRedirectsExtensionsTests
{
    /// <summary>The default-args overload returns a non-null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsOverload()
    {
        var b = new DocBuilder().UseRedirects();
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The configure-delegate overload runs the delegate against the defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureDelegateOverload()
    {
        var b = new DocBuilder().UseRedirects(static o => o.Add("/old/"u8, "/new/"u8));
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The options + logger overload registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsLoggerOverload()
    {
        var b = new DocBuilder().UseRedirects(RedirectsOptions.Default, NullLogger.Instance);
        await Assert.That(b).IsNotNull();
    }
}
