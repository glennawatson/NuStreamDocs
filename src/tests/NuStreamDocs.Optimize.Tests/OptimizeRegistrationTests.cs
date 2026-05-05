// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Optimize.Tests;

/// <summary>Builder-extension tests for the optimize plugins.</summary>
public class OptimizeRegistrationTests
{
    /// <summary>UseOptimize() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimizeRegisters() =>
        await Assert.That(new DocBuilder().UseOptimize()).IsTypeOf<DocBuilder>();

    /// <summary>UseOptimize(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimizeOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseOptimize(OptimizeOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseOptimize(options, logger) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimizeLoggerRegisters() =>
        await Assert.That(new DocBuilder().UseOptimize(OptimizeOptions.Default, NullLogger.Instance)).IsTypeOf<DocBuilder>();

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
        await Assert.That(new HtmlMinifyPlugin().Name.SequenceEqual("html-minify"u8)).IsTrue();

    /// <summary>HtmlMinifyPlugin minifies HTML in the post-resolve hook.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlMinifyRewrite()
    {
        ArrayBufferWriter<byte> output = new(64);
        PagePostResolveContext ctx = new("p.md", "<p>   spaces   </p>"u8, output);
        new HtmlMinifyPlugin().Rewrite(in ctx);
        await Assert.That(Encoding.UTF8.GetString(output.WrittenSpan)).DoesNotContain("   spaces   ");
    }

    /// <summary>UseOptimize rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimizeRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderOptimizeExtensions.UseOptimize(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseOptimize(options) rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseOptimizeRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseOptimize(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseHtmlMinify rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHtmlMinifyRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderOptimizeExtensions.UseHtmlMinify(null!));
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
