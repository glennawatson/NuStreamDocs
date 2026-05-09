// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Arithmatex.MathJax.Tests;

/// <summary>Coverage tests for <see cref="MathJaxPlugin"/>.</summary>
public class MathJaxPluginTests
{
    /// <summary>Default head fragment includes the inline config + the CDN loader script.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultHeadIncludesConfigAndLoader()
    {
        MathJaxPlugin plugin = new();
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var rendered = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(rendered).Contains("window.MathJax");
        await Assert.That(rendered).Contains("processHtmlClass:'arithmatex'");
        await Assert.That(rendered).Contains("https://cdn.jsdelivr.net/npm/mathjax@3");
        await Assert.That(rendered).Contains("async></script>");
    }

    /// <summary>Tweaked options flow through to the rendered head.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomOptionsFlowThrough()
    {
        var opts = MathJaxOptions.Default
            .WithLoaderUrl("https://example.com/mathjax.js")
            .WithProcessHtmlClass("math")
            .WithIgnoreHtmlClass("no-math");

        MathJaxPlugin plugin = new(opts);
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var rendered = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(rendered).Contains("https://example.com/mathjax.js");
        await Assert.That(rendered).Contains("processHtmlClass:'math'");
        await Assert.That(rendered).Contains("ignoreHtmlClass:'no-math'");
    }

    /// <summary>Single quotes inside a class regex get escaped so the inline JS stays parseable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleQuotesInClassesAreEscaped()
    {
        var opts = MathJaxOptions.Default.WithProcessHtmlClass("foo'bar");
        MathJaxPlugin plugin = new(opts);
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var rendered = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(rendered).Contains(@"processHtmlClass:'foo\'bar'");
    }

    /// <summary><c>WriteHeadExtra</c> rejects a null writer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraNullWriterThrows() =>
        await Assert.That(() => new MathJaxPlugin().WriteHeadExtra(null!)).Throws<ArgumentNullException>();

    /// <summary><c>UseMathJax()</c> overloads register the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuilderExtensionsRegister()
    {
        var defaults = new DocBuilder().UseMathJax();
        await Assert.That(defaults).IsNotNull();

        var configured = new DocBuilder().UseMathJax(static o => o.WithProcessHtmlClass("math"));
        await Assert.That(configured).IsNotNull();

        var optionsOverload = new DocBuilder().UseMathJax(MathJaxOptions.Default);
        await Assert.That(optionsOverload).IsNotNull();
    }
}
