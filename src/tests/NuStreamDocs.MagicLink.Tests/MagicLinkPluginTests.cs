// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MagicLink.Tests;

/// <summary>Lifecycle / registration tests for <c>MagicLinkPlugin</c>.</summary>
public class MagicLinkPluginTests
{
    /// <summary>PreRender wraps a bare URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreRenderWrapsBareUrl()
    {
        ArrayBufferWriter<byte> sink = new(64);
        PagePreRenderContext ctx = new("p.md", "see https://example.com here"u8, sink);
        new MagicLinkPlugin().PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("<https://example.com>");
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new MagicLinkPlugin().Name.SequenceEqual("magiclink"u8)).IsTrue();

    /// <summary>UseMagicLink registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMagicLinkRegisters()
    {
        DocBuilder builder = new();
        await Assert.That(builder.UseMagicLink()).IsSameReferenceAs(builder);
    }

    /// <summary>UseMagicLink rejects null builders.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMagicLinkRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderMagicLinkExtensions.UseMagicLink(null!));
        await Assert.That(ex).IsNotNull();
    }
}
