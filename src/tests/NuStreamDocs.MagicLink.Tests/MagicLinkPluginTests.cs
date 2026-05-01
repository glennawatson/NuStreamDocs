// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.MagicLink.Tests;

/// <summary>Lifecycle / registration tests for <c>MagicLinkPlugin</c>.</summary>
public class MagicLinkPluginTests
{
    /// <summary>Preprocess wraps a bare URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessWrapsBareUrl()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        new MagicLinkPlugin().Preprocess("see https://example.com here"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("<https://example.com>");
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new MagicLinkPlugin().Name).IsEqualTo("magiclink");

    /// <summary>Preprocess rejects null sinks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessRejectsNullSink()
    {
        var plugin = new MagicLinkPlugin();
        var ex = Assert.Throws<ArgumentNullException>(() => plugin.Preprocess(default, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseMagicLink registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMagicLinkRegisters()
    {
        var builder = new DocBuilder();
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
