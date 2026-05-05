// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences.Tests;

/// <summary>Behavior tests for <c>SuperFencesDispatcher</c>.</summary>
public class SuperFencesDispatcherTests
{
    /// <summary>A fence whose language matches a registered handler is replaced wholesale.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RegisteredHandlerReplacesBlock()
    {
        const string Source = "before <pre><code class=\"language-mermaid\">graph TD; A--&gt;B</code></pre> after";
        const string Expected = "before <div class=\"mermaid\">graph TD; A-->B</div> after";
        await Assert.That(Dispatch(Source, new MermaidStubHandler())).IsEqualTo(Expected);
    }

    /// <summary>A fence whose language has no handler passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnregisteredLanguagePassesThrough()
    {
        const string Source = "<pre><code class=\"language-csharp\">var x = 1;</code></pre>";
        await Assert.That(Dispatch(Source, new MermaidStubHandler())).IsEqualTo(Source);
    }

    /// <summary>HTML lacking any candidate fence shape passes through and short-circuits the scan.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlWithoutFencesIsUnchanged()
    {
        const string Source = "<p>hello world</p>";
        await Assert.That(Dispatch(Source, new MermaidStubHandler())).IsEqualTo(Source);
    }

    /// <summary>Multiple registered handlers each fire on their own fence.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleHandlersDispatchIndependently()
    {
        const string Source = "<pre><code class=\"language-mermaid\">A</code></pre> mid <pre><code class=\"language-math\">x</code></pre>";
        const string Expected = "<div class=\"mermaid\">A</div> mid <div class=\"math\">x</div>";
        ICustomFenceHandler[] handlers = [new MermaidStubHandler(), new MathStubHandler()];
        await Assert.That(Dispatch(Source, handlers)).IsEqualTo(Expected);
    }

    /// <summary>HTML entities in the body are decoded before being handed to the handler.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EntitiesAreDecodedForHandlers()
    {
        const string Source = "<pre><code class=\"language-mermaid\">if &amp;&amp; &quot;ok&quot;</code></pre>";
        const string Expected = "<div class=\"mermaid\">if && \"ok\"</div>";
        await Assert.That(Dispatch(Source, new MermaidStubHandler())).IsEqualTo(Expected);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Dispatch(string.Empty, new MermaidStubHandler())).IsEqualTo(string.Empty);

    /// <summary>Drives bytes through the dispatcher with a single handler.</summary>
    /// <param name="input">HTML input.</param>
    /// <param name="handler">Handler.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Dispatch(string input, ICustomFenceHandler handler) => Dispatch(input, [handler]);

    /// <summary>Drives bytes through the dispatcher with a list of handlers.</summary>
    /// <param name="input">HTML input.</param>
    /// <param name="handlers">Handlers.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string Dispatch(string input, ICustomFenceHandler[] handlers)
    {
        Dictionary<byte[], ICustomFenceHandler> index = new(handlers.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < handlers.Length; i++)
        {
            index[handlers[i].Language.ToArray()] = handlers[i];
        }

        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        if (!SuperFencesDispatcher.DispatchInto(bytes, index.GetAlternateLookup<ReadOnlySpan<byte>>(), sink))
        {
            sink.Write(bytes);
        }

        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }

    /// <summary>Stub handler for the <c>mermaid</c> fence; emits a simple <c>div</c> wrapper.</summary>
    private sealed class MermaidStubHandler : ICustomFenceHandler
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => "mermaid"u8;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            writer.Write("<div class=\"mermaid\">"u8);
            writer.Write(content);
            writer.Write("</div>"u8);
        }
    }

    /// <summary>Stub handler for the <c>math</c> fence.</summary>
    private sealed class MathStubHandler : ICustomFenceHandler
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => "math"u8;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            writer.Write("<div class=\"math\">"u8);
            writer.Write(content);
            writer.Write("</div>"u8);
        }
    }
}
