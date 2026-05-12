// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Transitions.Tests;

/// <summary>Coverage for <c>DocBuilderTransitionsExtensions</c> overloads.</summary>
public class DocBuilderTransitionsExtensionsTests
{
    /// <summary>The default-args overload returns a non-null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsOverload()
    {
        var b = new DocBuilder().UseTransitions();
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The configure-delegate overload runs the delegate against the defaults.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureDelegateOverload()
    {
        var b = new DocBuilder().UseTransitions(static o => o.WithPrefetch(PrefetchStrategy.Viewport));
        await Assert.That(b).IsNotNull();
    }

    /// <summary>The options overload registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OptionsOverload()
    {
        var b = new DocBuilder().UseTransitions(TransitionsOptions.Default.WithoutPrefetch());
        await Assert.That(b).IsNotNull();
    }
}
