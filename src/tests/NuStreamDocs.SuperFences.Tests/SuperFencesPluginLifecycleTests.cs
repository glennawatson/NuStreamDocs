// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences.Tests;

/// <summary>End-to-end lifecycle tests for <c>SuperFencesPlugin</c>.</summary>
public class SuperFencesPluginLifecycleTests
{
    /// <summary>Without ConfigureAsync the plugin is signalled as no-op via NeedsRewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpBeforeConfigure() =>
        await Assert.That(new SuperFencesPlugin().NeedsRewrite("<pre><code class=\"language-mermaid\">x</code></pre>"u8)).IsFalse();

    /// <summary>ConfigureAsync discovers ICustomFenceHandler plugins and dispatches matching blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DiscoversHandlersAndDispatches()
    {
        var plugin = new SuperFencesPlugin();
        var stub = new StubHandler();
        var ctx = new BuildConfigureContext("/in", "/out", [plugin, stub], new());
        await plugin.ConfigureAsync(ctx, CancellationToken.None);

        var output = RunPostRender(plugin, "<pre><code class=\"language-stub\">body</code></pre>"u8);
        await Assert.That(Encoding.UTF8.GetString(output)).Contains("<stub>body</stub>");
    }

    /// <summary>HTML without any fence blocks short-circuits before dispatch.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoFenceMarkup()
    {
        var plugin = new SuperFencesPlugin();
        var ctx = new BuildConfigureContext("/in", "/out", [plugin, new StubHandler()], new());
        await plugin.ConfigureAsync(ctx, CancellationToken.None);

        await Assert.That(plugin.NeedsRewrite("<p>plain</p>"u8)).IsFalse();
    }

    /// <summary>An empty handler set is a no-op even when fences are present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoOpWhenNoHandlers()
    {
        var plugin = new SuperFencesPlugin();
        var ctx = new BuildConfigureContext("/in", "/out", [plugin], new());
        await plugin.ConfigureAsync(ctx, CancellationToken.None);

        await Assert.That(plugin.NeedsRewrite("<pre><code class=\"language-stub\">x</code></pre>"u8)).IsFalse();
    }

    /// <summary>Handlers that report a null/empty Language are silently dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyLanguageHandlerIgnored()
    {
        var plugin = new SuperFencesPlugin();
        var bad = new EmptyLanguageHandler();
        var ctx = new BuildConfigureContext("/in", "/out", [plugin, bad], new());
        await plugin.ConfigureAsync(ctx, CancellationToken.None);

        await Assert.That(plugin.NeedsRewrite("<pre><code class=\"language-stub\">x</code></pre>"u8)).IsFalse();
    }

    /// <summary>Drives one PostRender call against a fresh sink and returns the rewritten bytes.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="html">Input HTML bytes.</param>
    /// <returns>Rewritten output bytes.</returns>
    private static byte[] RunPostRender(SuperFencesPlugin plugin, ReadOnlySpan<byte> html)
    {
        var output = new ArrayBufferWriter<byte>(128);
        var ctx = new PagePostRenderContext("p.md", default, html, output);
        plugin.PostRender(in ctx);
        return [.. output.WrittenSpan];
    }

    /// <summary>Stub fence handler used by the dispatch path tests.</summary>
    private sealed class StubHandler : IPlugin, ICustomFenceHandler
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "stub-handler"u8;

        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => "stub"u8;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.Write("<stub>"u8);
            writer.Write(content);
            writer.Write("</stub>"u8);
        }
    }

    /// <summary>Handler that reports an empty language; should be filtered out.</summary>
    private sealed class EmptyLanguageHandler : IPlugin, ICustomFenceHandler
    {
        /// <inheritdoc/>
        public ReadOnlySpan<byte> Name => "empty-lang"u8;

        /// <inheritdoc/>
        public ReadOnlySpan<byte> Language => default;

        /// <inheritdoc/>
        public void Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
        {
            // Should never be called.
            _ = content;
            _ = writer;
        }
    }
}
