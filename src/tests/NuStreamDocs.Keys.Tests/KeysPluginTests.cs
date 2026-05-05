// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Keys.Tests;

/// <summary>Lifecycle / registration tests for <c>KeysPlugin</c>.</summary>
public class KeysPluginTests
{
    /// <summary>PreRender wraps a key combo into kbd elements.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreRenderWrapsKeys()
    {
        ArrayBufferWriter<byte> sink = new(64);
        PagePreRenderContext ctx = new("p.md", "press ++ctrl+c++"u8, sink);
        new KeysPlugin().PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("<kbd");
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new KeysPlugin().Name.SequenceEqual("keys"u8)).IsTrue();

    /// <summary>UseKeys registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseKeysRegisters()
    {
        DocBuilder builder = new();
        await Assert.That(builder.UseKeys()).IsSameReferenceAs(builder);
    }

    /// <summary>UseKeys rejects a null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseKeysRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderKeysExtensions.UseKeys(null!));
        await Assert.That(ex).IsNotNull();
    }
}
