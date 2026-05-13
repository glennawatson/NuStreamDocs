// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.SuperFences;

/// <summary>
/// Custom-fence dispatcher (pymdownx superfences). Plugins that also implement
/// <see cref="ICustomFenceHandler"/> claim a language; matched
/// <c>&lt;pre&gt;&lt;code class="language-{lang}"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks are replaced with the handler's rendering. Handlers receive the fence
/// body with HTML entities decoded back to their literal bytes.
/// </summary>
public sealed class SuperFencesPlugin : IBuildConfigurePlugin, IPagePostRenderPlugin
{
    /// <summary>Handler index keyed on language bytes; built during configure.</summary>
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
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => _handlers is not null && _handlers.Count is not 0 &&
                                                         SuperFencesDispatcher.NeedsDispatch(html);

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
