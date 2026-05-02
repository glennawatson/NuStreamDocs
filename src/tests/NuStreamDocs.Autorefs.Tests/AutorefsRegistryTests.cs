// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Behavior tests for <c>AutorefsRegistry</c>.</summary>
public class AutorefsRegistryTests
{
    /// <summary>Registering with a fragment yields a hash-suffixed URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegisterWithFragmentYieldsSuffix()
    {
        var registry = new AutorefsRegistry();
        registry.Register("intro", "guide/intro.html", "intro");

        await Assert.That(registry.TryResolve("intro", out var url)).IsTrue();
        await Assert.That(url).IsEqualTo("guide/intro.html#intro");
    }

    /// <summary>Registering without a fragment yields a bare URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegisterWithoutFragmentYieldsBareUrl()
    {
        var registry = new AutorefsRegistry();
        registry.Register("home", "index.html", null);

        await Assert.That(registry.TryResolve("home", out var url)).IsTrue();
        await Assert.That(url).IsEqualTo("index.html");
    }

    /// <summary>Resolving an unknown ID reports a miss.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryResolveReportsMiss()
    {
        var registry = new AutorefsRegistry();
        await Assert.That(registry.TryResolve("missing", out _)).IsFalse();
    }

    /// <summary>Last write wins on duplicate IDs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LastWriteWins()
    {
        var registry = new AutorefsRegistry();
        registry.Register("Foo", "a.html", "Foo");
        registry.Register("Foo", "b.html", "Foo");

        await Assert.That(registry.TryResolve("Foo", out var url)).IsTrue();
        await Assert.That(url).IsEqualTo("b.html#Foo");
    }
}
