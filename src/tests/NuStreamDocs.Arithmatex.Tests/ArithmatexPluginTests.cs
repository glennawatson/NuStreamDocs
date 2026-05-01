// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Arithmatex.Tests;

/// <summary>Lifecycle / registration tests for <c>ArithmatexPlugin</c>.</summary>
public class ArithmatexPluginTests
{
    /// <summary>Preprocess wraps inline math.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessWrapsInlineMath()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        new ArithmatexPlugin().Preprocess("solve $x+1$"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("arithmatex");
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new ArithmatexPlugin().Name).IsEqualTo("arithmatex");

    /// <summary>Preprocess rejects null sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreprocessRejectsNullSink()
    {
        var plugin = new ArithmatexPlugin();
        var ex = Assert.Throws<ArgumentNullException>(() => plugin.Preprocess(default, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseArithmatex registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseArithmatexRegisters()
    {
        var builder = new DocBuilder();
        await Assert.That(builder.UseArithmatex()).IsSameReferenceAs(builder);
    }

    /// <summary>UseArithmatex rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseArithmatexRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderArithmatexExtensions.UseArithmatex(null!));
        await Assert.That(ex).IsNotNull();
    }
}
