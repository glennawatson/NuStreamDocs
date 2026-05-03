// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Lightbox.Tests;

/// <summary>Behavior tests for <c>LightboxPlugin</c>.</summary>
public class LightboxPluginTests
{
    /// <summary>Head extras emit the configured CSS link and JS bootstrap.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HeadExtrasIncludeStylesheetAndScript()
    {
        var plugin = new LightboxPlugin();
        var sink = new ArrayBufferWriter<byte>();
        plugin.WriteHeadExtra(sink);

        var html = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(html.Contains("glightbox.min.css", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("glightbox.min.js", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("GLightbox", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>When wrapping is disabled, no head bootstrap script is missed but the bytes are inert.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WrapImagesFalseLeavesHeadIntact()
    {
        var plugin = new LightboxPlugin(LightboxOptions.Default with { WrapImages = false });
        var sink = new ArrayBufferWriter<byte>();
        plugin.WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsGreaterThan(0);
    }

    /// <summary>Empty stylesheet URL skips the stylesheet link.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyStylesheetSkipsLink()
    {
        var sink = new ArrayBufferWriter<byte>();
        new LightboxPlugin(LightboxOptions.Default with { StylesheetUrl = [] }).WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).DoesNotContain("<link rel=\"stylesheet\"");
        await Assert.That(head).Contains("<script defer");
    }

    /// <summary>Empty script URL skips the script + bootstrap blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyScriptSkipsScript()
    {
        var sink = new ArrayBufferWriter<byte>();
        new LightboxPlugin(LightboxOptions.Default with { ScriptUrl = [] }).WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("<link rel=\"stylesheet\"");
        await Assert.That(head).DoesNotContain("<script");
    }

    /// <summary>WriteHeadExtra rejects a null writer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraNullWriterThrows() =>
        await Assert.That(() => new LightboxPlugin().WriteHeadExtra(null!)).Throws<ArgumentNullException>();

    /// <summary>Constructor rejects a null options instance.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullOptionsThrows() =>
        await Assert.That(() => new LightboxPlugin(null!)).Throws<ArgumentNullException>();
}
