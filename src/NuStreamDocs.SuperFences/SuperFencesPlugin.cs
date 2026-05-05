// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences;

/// <summary>
/// Custom-fence dispatcher (pymdownx superfences). Discovers
/// every <see cref="ICustomFenceHandler"/> exposed by registered
/// plugins during <see cref="ConfigureAsync"/>, then walks each
/// rendered page in <see cref="PostRender"/> looking for
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
/// dispatch loop never UTF-16 transcodes a language identifier.
/// </remarks>
public sealed class SuperFencesPlugin : IBuildConfigurePlugin, IPagePostRenderPlugin
{
    /// <summary>Resolved handler index, keyed on the language bytes. Built once during <see cref="ConfigureAsync"/>.</summary>
    private Dictionary<byte[], ICustomFenceHandler>? _handlers;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "superfences"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var plugins = context.Plugins;
        Dictionary<byte[], ICustomFenceHandler> seed = new(plugins.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is not ICustomFenceHandler handler)
            {
                continue;
            }

            var language = handler.Language;
            if (language.IsEmpty)
            {
                continue;
            }

            seed[language.ToArray()] = handler;
        }

        _handlers = seed;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => _handlers is not null && _handlers.Count is not 0 && SuperFencesDispatcher.NeedsDispatch(html);

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var handlers = _handlers!;
        var altLookup = handlers.AsUtf8Lookup();
        if (SuperFencesDispatcher.DispatchInto(context.Html, altLookup, context.Output))
        {
            return;
        }

        // No handler matched a candidate block — the engine still expects the
        // full page in the output sink, so copy the input through verbatim.
        context.Output.Write(context.Html);
    }
}
