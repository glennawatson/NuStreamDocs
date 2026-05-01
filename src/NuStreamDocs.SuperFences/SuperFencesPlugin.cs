// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
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
/// literal bytes.
/// </remarks>
public sealed class SuperFencesPlugin : DocPluginBase
{
    /// <summary>Resolved handler index, keyed on language name. Built once during <see cref="OnConfigureAsync"/>.</summary>
    private FrozenDictionary<string, ICustomFenceHandler>? _handlers;

    /// <inheritdoc/>
    public override string Name => "superfences";

    /// <inheritdoc/>
    public override ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var plugins = context.Plugins;
        var seed = new Dictionary<string, ICustomFenceHandler>(StringComparer.Ordinal);
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is ICustomFenceHandler handler && handler.Language is { Length: > 0 })
            {
                seed[handler.Language] = handler;
            }
        }

        _handlers = seed.ToFrozenDictionary(StringComparer.Ordinal);

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
        var rendered = html.WrittenSpan;
        if (!SuperFencesDispatcher.NeedsDispatch(rendered))
        {
            return ValueTask.CompletedTask;
        }

        var rewritten = SuperFencesDispatcher.Dispatch(rendered, handlers);
        if (rewritten.Length == rendered.Length && rewritten.AsSpan().SequenceEqual(rendered))
        {
            return ValueTask.CompletedTask;
        }

        html.ResetWrittenCount();
        html.Write(rewritten);

        return ValueTask.CompletedTask;
    }
}
