// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Mermaid.Tests;

/// <summary>Lifecycle / registration tests for <c>MermaidPlugin</c>.</summary>
public class MermaidPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new MermaidPlugin().Name.SequenceEqual("mermaid"u8)).IsTrue();

    /// <summary>PostRender rewrites <c>language-mermaid</c> code blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PostRenderRetagsMermaidBlock()
    {
        var output = RunPostRender(new(), "<pre><code class=\"language-mermaid\">graph TD\nA-->B</code></pre>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("<pre class=\"mermaid\">");
    }

    /// <summary>HTML without a mermaid block is signalled as no-op via NeedsRewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PostRenderNoOpWhenAbsent() =>
        await Assert.That(new MermaidPlugin().NeedsRewrite("<p>plain</p>"u8)).IsFalse();

    /// <summary>WriteHeadExtra emits a script tag pulling in the runtime.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraEmitsScript()
    {
        var sink = new ArrayBufferWriter<byte>(256);
        new MermaidPlugin().WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("mermaid");
        await Assert.That(head).Contains("<script");
    }

    /// <summary>WriteHeadExtra rejects null sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraRejectsNullSink()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new MermaidPlugin().WriteHeadExtra(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>The custom fence handler emits a <c>pre.mermaid</c> wrapper.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomFenceRenderEmitsWrapper()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        ICustomFenceHandler handler = new MermaidPlugin();
        handler.Render("graph TD\nA-->B"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("<pre class=\"mermaid\">graph TD\nA-->B</pre>");
    }

    /// <summary>The custom fence handler reports the language.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomFenceLanguageIsMermaid()
    {
        ICustomFenceHandler handler = new MermaidPlugin();
        await Assert.That(handler.Language.SequenceEqual("mermaid"u8)).IsTrue();
    }

    /// <summary>Custom fence Render rejects null sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomFenceRenderRejectsNullSink()
    {
        ICustomFenceHandler handler = new MermaidPlugin();
        var ex = Assert.Throws<ArgumentNullException>(() => handler.Render(default, null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseMermaid registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMermaidRegisters() =>
        await Assert.That(new DocBuilder().UseMermaid()).IsTypeOf<DocBuilder>();

    /// <summary>UseMermaid rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMermaidRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderMermaidExtensions.UseMermaid(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(MermaidPlugin plugin, ReadOnlySpan<byte> html)
    {
        var output = new ArrayBufferWriter<byte>(128);
        var ctx = new PagePostRenderContext("page.md", default, html, output);
        plugin.PostRender(in ctx);
        return [.. output.WrittenSpan];
    }
}
