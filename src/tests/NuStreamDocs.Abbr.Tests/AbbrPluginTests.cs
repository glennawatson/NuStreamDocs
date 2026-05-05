// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Abbr.Tests;

/// <summary>Lifecycle / registration tests for the abbreviation plugin.</summary>
public class AbbrPluginTests
{
    /// <summary>PreRender wraps a known token in <c>&lt;abbr&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PreRenderWrapsToken()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        var ctx = new PagePreRenderContext("p.md", "HTML rules.\n\n*[HTML]: Hyper Text Markup Language\n"u8, sink);
        new AbbrPlugin().PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan))
            .Contains("<abbr title=\"Hyper Text Markup Language\">HTML</abbr>");
    }

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() =>
        await Assert.That(new AbbrPlugin().Name.SequenceEqual("abbr"u8)).IsTrue();

    /// <summary>UseAbbreviations registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAbbreviationsRegisters()
    {
        var builder = new DocBuilder();
        await Assert.That(builder.UseAbbreviations()).IsSameReferenceAs(builder);
    }

    /// <summary>UseAbbreviations rejects a null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseAbbreviationsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderAbbrExtensions.UseAbbreviations(null!));
        await Assert.That(ex).IsNotNull();
    }
}
