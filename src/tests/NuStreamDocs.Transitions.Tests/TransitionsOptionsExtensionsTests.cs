// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Transitions.Tests;

/// <summary>Coverage for <c>TransitionsOptionsExtensions</c>.</summary>
public class TransitionsOptionsExtensionsTests
{
    /// <summary>The defaults are the expected content selector, hover prefetch, and fade animation.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultsAreSane()
    {
        var o = TransitionsOptions.Default;
        await Assert.That(Encoding.UTF8.GetString(o.ContentSelector)).IsEqualTo("[data-md-component='content']");
        await Assert.That(o.NavSelector.Length).IsEqualTo(0);
        await Assert.That(o.Prefetch).IsEqualTo(PrefetchStrategy.Hover);
        await Assert.That(o.Animation).IsEqualTo(TransitionAnimation.Fade);
        await Assert.That(o.PrefetchDelayMs).IsEqualTo(80);
        await Assert.That(o.Enabled).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(o.IgnoreSelector)).Contains("download");
    }

    /// <summary>The fluent setters round-trip.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SettersRoundTrip()
    {
        var o = TransitionsOptions.Default
            .WithContentSelector("main"u8)
            .WithNavSelector(".sidebar"u8)
            .WithAnimation(TransitionAnimation.None)
            .WithPrefetch(PrefetchStrategy.Viewport)
            .WithPrefetchDelay(150)
            .WithIgnoreSelector(".no-router"u8);
        await Assert.That(Encoding.UTF8.GetString(o.ContentSelector)).IsEqualTo("main");
        await Assert.That(Encoding.UTF8.GetString(o.NavSelector)).IsEqualTo(".sidebar");
        await Assert.That(o.Animation).IsEqualTo(TransitionAnimation.None);
        await Assert.That(o.Prefetch).IsEqualTo(PrefetchStrategy.Viewport);
        await Assert.That(o.PrefetchDelayMs).IsEqualTo(150);
        await Assert.That(Encoding.UTF8.GetString(o.IgnoreSelector)).IsEqualTo(".no-router");
    }

    /// <summary><c>WithoutPrefetch</c> and <c>Disable</c> toggle their flags.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WithoutPrefetchAndDisable()
    {
        await Assert.That(TransitionsOptions.Default.WithoutPrefetch().Prefetch).IsEqualTo(PrefetchStrategy.Off);
        await Assert.That(TransitionsOptions.Default.Disable().Enabled).IsFalse();
    }
}
