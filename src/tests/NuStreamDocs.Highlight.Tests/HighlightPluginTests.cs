// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Highlight.Tests;

/// <summary>Lifecycle / registration tests for <c>HighlightPlugin</c>.</summary>
public class HighlightPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new HighlightPlugin().Name).IsEqualTo("highlight");

    /// <summary>Default options has no extra lexers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptions() =>
        await Assert.That(HighlightOptions.Default.ExtraLexers.Length).IsEqualTo(0);

    /// <summary>OnRenderPageAsync is a no-op when no <c>language-X</c> code block is present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoCodeBlock()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        const string Html = "<p>plain</p>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        await new HighlightPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
    }

    /// <summary>OnRenderPageAsync rewrites a known language block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RewritesKnownLanguageBlock()
    {
        var sink = new ArrayBufferWriter<byte>(128);
        sink.Write(Encoding.UTF8.GetBytes("<pre><code class=\"language-csharp\">int x = 1;</code></pre>"));
        await new HighlightPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("class=\"kt\"");
    }

    /// <summary>Unknown languages are passed through untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownLanguagePassesThrough()
    {
        var sink = new ArrayBufferWriter<byte>(128);
        const string Html = "<pre><code class=\"language-zzz\">just text</code></pre>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        await new HighlightPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
    }

    /// <summary>OnFinaliseAsync is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OnFinaliseAsyncNoOp() =>
        await new HighlightPlugin().OnFinaliseAsync(new("/out"), CancellationToken.None);

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
}
