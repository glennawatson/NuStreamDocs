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
    /// <summary>Initial capacity for the script element.</summary>
    private const int HeadOutputCapacity = 256;

    /// <summary>Initial capacity for a rendered code fence.</summary>
    private const int FenceOutputCapacity = 64;

    /// <summary>Initial capacity for rewritten HTML.</summary>
    private const int RewriteOutputCapacity = 128;

    /// <summary>Gets the diagram language identifier.</summary>
    private static ReadOnlySpan<byte> MermaidLanguage => "mermaid"u8;

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new MermaidPlugin().Name.SequenceEqual(MermaidLanguage)).IsTrue();

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
        ArrayBufferWriter<byte> sink = new(HeadOutputCapacity);
        new MermaidPlugin().WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("mermaid");
        await Assert.That(head).Contains("<script");
    }

    /// <summary>The default head script opts out of Cloudflare Rocket Loader.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraOptsOutOfRocketLoaderByDefault()
    {
        ArrayBufferWriter<byte> sink = new(HeadOutputCapacity);
        new MermaidPlugin().WriteHeadExtra(sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan))
            .StartsWith("<script type=\"module\" data-cfasync=\"false\">");
    }

    /// <summary>The default runtime is the pinned mermaid ES module, configured with the page font.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraImportsPinnedRuntimeWithPageFont()
    {
        ArrayBufferWriter<byte> sink = new(HeadOutputCapacity);
        new MermaidPlugin().WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("import mermaid from \"https://cdn.jsdelivr.net/npm/mermaid@12.0.0/dist/mermaid.esm.min.mjs\";\n");
        await Assert.That(head).Contains("getComputedStyle(document.querySelector(\".md-typeset\") ?? document.body).fontFamily");
        await Assert.That(head).Contains("mermaid.initialize({ startOnLoad: true, fontFamily, themeVariables: { fontFamily } });");
        await Assert.That(head).EndsWith("</script>");
    }

    /// <summary>A configured runtime URL replaces the default pin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraUsesConfiguredRuntimeUrl()
    {
        ArrayBufferWriter<byte> sink = new(HeadOutputCapacity);
        new MermaidPlugin(MermaidOptions.Default with { RuntimeUrl = [.. "/assets/mermaid.mjs"u8] }).WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).Contains("import mermaid from \"/assets/mermaid.mjs\";");
        await Assert.That(head).DoesNotContain("cdn.jsdelivr.net");
    }

    /// <summary>Disabling the opt-out emits a plain module script.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraOmitsRocketLoaderOptOutWhenDisabled()
    {
        ArrayBufferWriter<byte> sink = new(HeadOutputCapacity);
        new MermaidPlugin(new MermaidOptions(false)).WriteHeadExtra(sink);
        var head = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(head).StartsWith("<script type=\"module\">");
        await Assert.That(head).DoesNotContain("data-cfasync");
    }

    /// <summary>UseMermaid with options registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMermaidWithOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseMermaid(MermaidOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>The custom fence handler emits a <c>pre.mermaid</c> wrapper.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomFenceRenderEmitsWrapper()
    {
        ArrayBufferWriter<byte> sink = new(FenceOutputCapacity);
        ICustomFenceHandler handler = new MermaidPlugin();
        handler.Render("graph TD\nA-->B"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan))
            .IsEqualTo("<pre class=\"mermaid\">graph TD\nA-->B</pre>");
    }

    /// <summary>The custom fence handler reports the language.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomFenceLanguageIsMermaid()
    {
        ICustomFenceHandler handler = new MermaidPlugin();
        await Assert.That(handler.Language.SequenceEqual(MermaidLanguage)).IsTrue();
    }

    /// <summary>UseMermaid registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMermaidRegisters() =>
        await Assert.That(new DocBuilder().UseMermaid()).IsTypeOf<DocBuilder>();

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(MermaidPlugin plugin, ReadOnlySpan<byte> html)
    {
        ArrayBufferWriter<byte> output = new(RewriteOutputCapacity);
        PagePostRenderContext ctx = new("page.md", default, html, output);
        plugin.PostRender(in ctx);
        return [.. output.WrittenSpan];
    }
}
