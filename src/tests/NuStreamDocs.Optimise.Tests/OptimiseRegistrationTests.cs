// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Optimise.Tests;

/// <summary>Builder-extension tests for the optimise plugins.</summary>
public class OptimiseRegistrationTests
{
    /// <summary>UseOptimise() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimiseRegisters() =>
        await Assert.That(new DocBuilder().UseOptimise()).IsTypeOf<DocBuilder>();

    /// <summary>UseOptimise(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimiseOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseOptimise(OptimiseOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseOptimise(options, logger) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimiseLoggerRegisters() =>
        await Assert.That(new DocBuilder().UseOptimise(OptimiseOptions.Default, NullLogger.Instance)).IsTypeOf<DocBuilder>();

    /// <summary>UseHtmlMinify() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHtmlMinifyRegisters() =>
        await Assert.That(new DocBuilder().UseHtmlMinify()).IsTypeOf<DocBuilder>();

    /// <summary>UseHtmlMinify(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHtmlMinifyOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseHtmlMinify(HtmlMinifyOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>HtmlMinifyPlugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyPluginName() =>
        await Assert.That(new HtmlMinifyPlugin().Name).IsEqualTo("html-minify");

    /// <summary>HtmlMinifyPlugin minifies HTML at finalise.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyOnRenderPage()
    {
        var sink = new System.Buffers.ArrayBufferWriter<byte>(64);
        sink.Write("<p>   spaces   </p>"u8);
        await new HtmlMinifyPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(System.Text.Encoding.UTF8.GetString(sink.WrittenSpan)).DoesNotContain("   spaces   ");
    }

    /// <summary>UseOptimise rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimiseRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderOptimiseExtensions.UseOptimise(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseOptimise(options) rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimiseRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseOptimise(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseHtmlMinify rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHtmlMinifyRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderOptimiseExtensions.UseHtmlMinify(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseHtmlMinify(options) rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHtmlMinifyRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseHtmlMinify(null!));
        await Assert.That(ex).IsNotNull();
    }
}
