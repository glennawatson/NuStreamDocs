// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SmartSymbols.Tests;

/// <summary>Lifecycle / registration tests for <c>SmartSymbolsPlugin</c>.</summary>
public class SmartSymbolsPluginTests
{
    /// <summary>PreRender substitutes a known marker.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreRenderSubstitutes()
    {
        var sink = new ArrayBufferWriter<byte>(32);
        var ctx = new PagePreRenderContext("p.md", "(c) acme"u8, sink);
        new SmartSymbolsPlugin().PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("© acme");
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new SmartSymbolsPlugin().Name.SequenceEqual("smartsymbols"u8)).IsTrue();

    /// <summary>UseSmartSymbols registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSmartSymbolsRegisters()
    {
        var builder = new DocBuilder();
        await Assert.That(builder.UseSmartSymbols()).IsSameReferenceAs(builder);
    }

    /// <summary>UseSmartSymbols rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseSmartSymbolsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderSmartSymbolsExtensions.UseSmartSymbols(null!));
        await Assert.That(ex).IsNotNull();
    }
}
