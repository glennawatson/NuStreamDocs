// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Lifecycle / registration tests for <c>HighlightPlugin</c>.</summary>
public class HighlightPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new HighlightPlugin().Name.SequenceEqual("highlight"u8)).IsTrue();

    /// <summary>Default options has no extra lexers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptions() =>
        await Assert.That(HighlightOptions.Default.ExtraLexers.Length).IsEqualTo(0);

    /// <summary>NeedsRewrite returns false when no <c>language-X</c> code block is present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoCodeBlock() =>
        await Assert.That(new HighlightPlugin().NeedsRewrite("<p>plain</p>"u8)).IsFalse();

    /// <summary>PostRender rewrites a known language block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesKnownLanguageBlock()
    {
        var output = RunPostRender(new(), "<pre><code class=\"language-csharp\">int x = 1;</code></pre>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("class=\"kt\"");
    }

    /// <summary>Unknown-language bodies pass through verbatim (no token spans), but the wrapper still applies — matches Pygments / mkdocs-material output shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownLanguageBodyUnchanged()
    {
        var output = RunPostRender(new(), "<pre><code class=\"language-zzz\">just text</code></pre>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).IsEqualTo("<div class=\"highlight\"><pre><code class=\"language-zzz\">just text</code></pre></div>");
    }

    /// <summary>With <c>WrapInHighlightDiv = false</c>, the output preserves the original shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WrapDisabledPreservesOriginalShape()
    {
        const string Html = "<pre><code class=\"language-zzz\">just text</code></pre>";
        var plugin = new HighlightPlugin(HighlightOptions.Default with { WrapInHighlightDiv = false });
        var output = RunPostRender(plugin, Encoding.UTF8.GetBytes(Html));
        await Assert.That(Encoding.UTF8.GetString(output)).IsEqualTo(Html);
    }

    /// <summary>A <c>title="..."</c> in the fence-info string renders a <c>&lt;span class="filename"&gt;</c> above the block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TitleAttributeRendersFilenameSpan()
    {
        var output = RunPostRender(new(), "<pre><code class=\"language-zzz\" data-info=\"title=&quot;example.py&quot;\">x</code></pre>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("<span class=\"filename\">example.py</span>");
    }

    /// <summary>The copy button is opt-in via <see cref="HighlightOptions.CopyButton"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CopyButtonOptInEmitsButton()
    {
        var plugin = new HighlightPlugin(HighlightOptions.Default with { CopyButton = true });
        var output = RunPostRender(plugin, "<pre><code class=\"language-zzz\">x</code></pre>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("<button class=\"md-clipboard");
    }

    /// <summary>UseHighlight() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHighlightRegisters() =>
        await Assert.That(new DocBuilder().UseHighlight()).IsTypeOf<DocBuilder>();

    /// <summary>UseHighlight(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHighlightOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseHighlight(HighlightOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseHighlight rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHighlightRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderHighlightExtensions.UseHighlight(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseHighlight(options) rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseHighlightRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseHighlight(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>HighlightPlugin ctor rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CtorRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => _ = new HighlightPlugin(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(HighlightPlugin plugin, ReadOnlySpan<byte> html)
    {
        var output = new ArrayBufferWriter<byte>(128);
        var ctx = new PagePostRenderContext("p.md", default, html, output);
        plugin.PostRender(in ctx);
        return [.. output.WrittenSpan];
    }
}
