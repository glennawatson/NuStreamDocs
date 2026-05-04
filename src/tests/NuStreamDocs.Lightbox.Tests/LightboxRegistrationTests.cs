// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Lightbox.Tests;

/// <summary>Builder-extension + lifecycle tests for <c>LightboxPlugin</c>.</summary>
public class LightboxRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new LightboxPlugin().Name.AsSpan().SequenceEqual("lightbox"u8)).IsTrue();

    /// <summary>Defaults expose the expected CDN URLs and selector.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptions()
    {
        var defaults = LightboxOptions.Default;
        await Assert.That(Encoding.UTF8.GetString(defaults.StylesheetUrl)).Contains("glightbox");
        await Assert.That(defaults.WrapImages).IsTrue();
        await Assert.That(defaults.Selector).IsEqualTo("glightbox");
    }

    /// <summary>OnRenderPageAsync wraps a bare image when WrapImages is true.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WrapsBareImages()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write("<p><img src=\"a.png\" alt=\"a\"></p>"u8);
        await new LightboxPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("glightbox");
    }

    /// <summary>OnRenderPageAsync leaves images alone when WrapImages is false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SkipsWhenWrapDisabled()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        const string Html = "<p><img src=\"a.png\" alt=\"a\"></p>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        var plugin = new LightboxPlugin(LightboxOptions.Default with { WrapImages = false });
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
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
}
