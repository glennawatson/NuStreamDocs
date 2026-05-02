// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences;

/// <summary>
/// Custom-fence dispatcher (pymdownx superfences). Discovers
/// every <see cref="ICustomFenceHandler"/> exposed by registered
/// plugins during <see cref="OnConfigureAsync"/>, then walks each
/// rendered page in <see cref="OnRenderPageAsync"/> looking for
/// <c>&lt;pre&gt;&lt;code class="language-{lang}"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks whose language is claimed by a handler. Matched blocks
/// are replaced with the handler's rendering.
/// </summary>
/// <remarks>
/// Plugins claim a fence by additionally implementing
/// <see cref="ICustomFenceHandler"/> — same composition pattern
/// the head-extras / static-asset providers use. Handlers receive
/// the fence body with HTML entities decoded back to their
/// literal bytes. The handler index is byte-keyed so the per-page
/// dispatch loop never UTF-16 transcodes a language identifier;
/// the rewrite streams straight into the page sink via
/// <see cref="HtmlSnapshotRewriter"/> instead of materializing an
/// intermediate replacement byte array.
/// </remarks>
public sealed class SuperFencesPlugin : DocPluginBase
{
    /// <summary>Resolved handler index, keyed on the language bytes. Built once during <see cref="OnConfigureAsync"/>.</summary>
    private Dictionary<byte[], ICustomFenceHandler>? _handlers;

    /// <inheritdoc/>
    public override string Name => "superfences";

    /// <inheritdoc/>
    public override ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var plugins = context.Plugins;
        var seed = new Dictionary<byte[], ICustomFenceHandler>(plugins.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is ICustomFenceHandler handler && handler.Language is { Length: > 0 })
            {
                seed[Encoding.UTF8.GetBytes(handler.Language)] = handler;
            }
        }

        _handlers = seed;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var handlers = _handlers;
        if (handlers is null || handlers.Count is 0)
        {
            return ValueTask.CompletedTask;
        }

        var html = context.Html;
        if (!SuperFencesDispatcher.NeedsDispatch(html.WrittenSpan))
        {
            return ValueTask.CompletedTask;
        }

        var altLookup = handlers.GetAlternateLookup<ReadOnlySpan<byte>>();
        HtmlSnapshotRewriter.Rewrite(html, altLookup, RewriteCallback);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Static callback that runs inside <see cref="HtmlSnapshotRewriter"/>.
    /// The dispatcher writes into <paramref name="sink"/> when it found
    /// at least one fence; on no-op we copy the snapshot back verbatim.
    /// </summary>
    /// <param name="snapshot">Read-only snapshot of the page bytes.</param>
    /// <param name="sink">Reset destination buffer.</param>
    /// <param name="lookup">Span-keyed handler lookup.</param>
    private static void RewriteCallback(
        ReadOnlySpan<byte> snapshot,
        ArrayBufferWriter<byte> sink,
        Dictionary<byte[], ICustomFenceHandler>.AlternateLookup<ReadOnlySpan<byte>> lookup)
    {
        if (SuperFencesDispatcher.DispatchInto(snapshot, lookup, sink))
        {
            return;
        }

        sink.Write(snapshot);
    }
}
