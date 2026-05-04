// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Keys.Tests;

/// <summary>Lifecycle / registration tests for <c>KeysPlugin</c>.</summary>
public class KeysPluginTests
{
    /// <summary>Preprocess wraps a key combo into kbd elements.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessWrapsKeys()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        new KeysPlugin().Preprocess("press ++ctrl+c++"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("<kbd");
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new KeysPlugin().Name.SequenceEqual("keys"u8)).IsTrue();

    /// <summary>Preprocess rejects a null sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessRejectsNullSink()
    {
        var plugin = new KeysPlugin();
        var ex = Assert.Throws<ArgumentNullException>(() => plugin.Preprocess(default, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseKeys registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseKeysRegisters()
    {
        var builder = new DocBuilder();
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
