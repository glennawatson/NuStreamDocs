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
        AutorefsRegistry registry = new();
        registry.Register("intro"u8, [.. "guide/intro.html"u8], "intro"u8);

        await Assert.That(registry.TryResolve("intro"u8, out var url)).IsTrue();
        await Assert.That(url.AsSpan().SequenceEqual("guide/intro.html#intro"u8)).IsTrue();
    }

    /// <summary>Registering without a fragment yields a bare URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RegisterWithoutFragmentYieldsBareUrl()
    {
        AutorefsRegistry registry = new();
        registry.Register("home"u8, [.. "index.html"u8], default);

        await Assert.That(registry.TryResolve("home"u8, out var url)).IsTrue();
        await Assert.That(url.AsSpan().SequenceEqual("index.html"u8)).IsTrue();
    }

    /// <summary>Resolving an unknown ID reports a miss.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryResolveReportsMiss()
    {
        AutorefsRegistry registry = new();
        await Assert.That(registry.TryResolve("missing"u8, out _)).IsFalse();
    }

    /// <summary>Last write wins on duplicate IDs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LastWriteWins()
    {
        AutorefsRegistry registry = new();
        registry.Register("Foo"u8, [.. "a.html"u8], "Foo"u8);
        registry.Register("Foo"u8, [.. "b.html"u8], "Foo"u8);

        await Assert.That(registry.TryResolve("Foo"u8, out var url)).IsTrue();
        await Assert.That(url.AsSpan().SequenceEqual("b.html#Foo"u8)).IsTrue();
    }
}
