// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Transitions.Tests;

/// <summary>Coverage for <see cref="TransitionsPlugin"/>.</summary>
public class TransitionsPluginTests
{
    /// <summary>The default plugin ships the router script asset and emits the config meta + script tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultEmitsAssetAndHeadExtra()
    {
        var plugin = new TransitionsPlugin();
        await Assert.That(plugin.Name.SequenceEqual("transitions"u8)).IsTrue();

        var assets = plugin.StaticAssets;
        await Assert.That(assets.Length).IsEqualTo(1);
        await Assert.That(assets[0].Path.Value).IsEqualTo("assets/javascripts/nstd-router.js");
        await Assert.That(assets[0].Bytes.Length).IsGreaterThan(2000);

        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("<meta name=\"nstd:router\" content=\"");
        await Assert.That(head).Contains("prefetch=hover");
        await Assert.That(head).Contains("animation=fade");
        await Assert.That(head).Contains("delay=80");

        // The default content selector uses single quotes, so it survives unescaped in the double-quoted attribute.
        await Assert.That(head).Contains("content=[data-md-component='content']");
        await Assert.That(head).Contains("<script src=\"/assets/javascripts/nstd-router.js\" defer></script>");
    }

    /// <summary>Custom options are reflected in the emitted config meta tag, and a <c>"</c> in a selector is escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomOptionsReflectedAndEscaped()
    {
        var plugin = new TransitionsPlugin(TransitionsOptions.Default
            .WithContentSelector("[data-x=\"y\"]"u8)
            .WithNavSelector(".side"u8)
            .WithPrefetch(PrefetchStrategy.Off)
            .WithAnimation(TransitionAnimation.None));
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("content=[data-x=&quot;y&quot;]");
        await Assert.That(head).Contains("nav=.side");
        await Assert.That(head).Contains("prefetch=off");
        await Assert.That(head).Contains("animation=none");
    }

    /// <summary>A disabled plugin contributes nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DisabledContributesNothing()
    {
        var plugin = new TransitionsPlugin(TransitionsOptions.Default.Disable());
        await Assert.That(plugin.StaticAssets.Length).IsEqualTo(0);
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }
}
