// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences.Tests;

/// <summary>End-to-end lifecycle tests for <c>SuperFencesPlugin</c>.</summary>
public class SuperFencesPluginLifecycleTests
{
    /// <summary>Without OnConfigureAsync the plugin is a no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpBeforeConfigure()
    {
        var sink = new ArrayBufferWriter<byte>(64);
        const string Html = "<pre><code class=\"language-mermaid\">x</code></pre>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        await new SuperFencesPlugin().OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
    }

    /// <summary>OnConfigureAsync discovers ICustomFenceHandler plugins and dispatches matching blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DiscoversHandlersAndDispatches()
    {
        var plugin = new SuperFencesPlugin();
        var stub = new StubHandler();
        var ctx = new PluginConfigureContext(default, "/in", "/out", [plugin, stub]);
        await plugin.OnConfigureAsync(ctx, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(128);
        const string Html = "<pre><code class=\"language-stub\">body</code></pre>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);

        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("<stub>body</stub>");
    }

    /// <summary>HTML without any fence blocks short-circuits before dispatch.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoFenceMarkup()
    {
        var plugin = new SuperFencesPlugin();
        var ctx = new PluginConfigureContext(default, "/in", "/out", [plugin, new StubHandler()]);
        await plugin.OnConfigureAsync(ctx, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(64);
        const string Html = "<p>plain</p>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
    }

    /// <summary>An empty handler set is a no-op even when fences are present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoHandlers()
    {
        var plugin = new SuperFencesPlugin();
        var ctx = new PluginConfigureContext(default, "/in", "/out", [plugin]);
        await plugin.OnConfigureAsync(ctx, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(64);
        const string Html = "<pre><code class=\"language-stub\">x</code></pre>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
    }

    /// <summary>Handlers that report a null/empty Language are silently dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyLanguageHandlerIgnored()
    {
        var plugin = new SuperFencesPlugin();
        var bad = new EmptyLanguageHandler();
        var ctx = new PluginConfigureContext(default, "/in", "/out", [plugin, bad]);
        await plugin.OnConfigureAsync(ctx, CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(64);
        const string Html = "<pre><code class=\"language-stub\">x</code></pre>";
        sink.Write(Encoding.UTF8.GetBytes(Html));
        await plugin.OnRenderPageAsync(new("p.md", default, sink), CancellationToken.None);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(Html);
    }

    /// <summary>Stub fence handler used by the dispatch path tests.</summary>
    private sealed class StubHandler : DocPluginBase, ICustomFenceHandler
    {
        /// <inheritdoc/>
        public override string Name => "stub-handler";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => "stub"u8;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            writer.Write("<stub>"u8);
            writer.Write(content);
            writer.Write("</stub>"u8);
        }
    }

    /// <summary>Handler that reports an empty language; should be filtered out.</summary>
    private sealed class EmptyLanguageHandler : DocPluginBase, ICustomFenceHandler
    {
        /// <inheritdoc/>
        public override string Name => "empty-lang";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => default;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            // Should never be called.
        }
    }
}
