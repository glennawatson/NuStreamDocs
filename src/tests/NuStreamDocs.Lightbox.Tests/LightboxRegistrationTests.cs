// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Lightbox.Tests;

/// <summary>Builder-extension + lifecycle tests for <c>LightboxPlugin</c>.</summary>
public class LightboxRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new LightboxPlugin().Name.SequenceEqual("lightbox"u8)).IsTrue();

    /// <summary>Defaults expose the expected CDN URLs and selector.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptions()
    {
        var defaults = LightboxOptions.Default;
        await Assert.That(Encoding.UTF8.GetString(defaults.StylesheetUrl)).Contains("glightbox");
        await Assert.That(defaults.WrapImages).IsTrue();
        await Assert.That(defaults.Selector.AsSpan().SequenceEqual("glightbox"u8)).IsTrue();
    }

    /// <summary>PostRender wraps a bare image when WrapImages is true.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WrapsBareImages()
    {
        var output = RunPostRender(new(), "<p><img src=\"a.png\" alt=\"a\"></p>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("glightbox");
    }

    /// <summary>NeedsRewrite returns false when WrapImages is false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipsWhenWrapDisabled()
    {
        var plugin = new LightboxPlugin(LightboxOptions.Default with { WrapImages = false });
        await Assert.That(plugin.NeedsRewrite("<p><img src=\"a.png\" alt=\"a\"></p>"u8)).IsFalse();
    }

    /// <summary>UseLightbox registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLightboxRegisters() =>
        await Assert.That(new DocBuilder().UseLightbox()).IsTypeOf<DocBuilder>();

    /// <summary>UseLightbox(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLightboxOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseLightbox(LightboxOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseLightbox rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLightboxRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderLightboxExtensions.UseLightbox(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseLightbox(options) rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLightboxOptionsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderLightboxExtensions.UseLightbox(null!, LightboxOptions.Default));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseLightbox(options) rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseLightboxOptionsRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseLightbox(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(LightboxPlugin plugin, ReadOnlySpan<byte> html)
    {
        var output = new ArrayBufferWriter<byte>(128);
        var ctx = new PagePostRenderContext("p.md", default, html, output);
        plugin.PostRender(in ctx);
        return [.. output.WrittenSpan];
    }
}
