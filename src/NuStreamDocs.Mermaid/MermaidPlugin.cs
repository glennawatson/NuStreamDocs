// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Mermaid;

/// <summary>
/// Mermaid diagram plugin. Rewrites
/// <c>&lt;pre&gt;&lt;code class="language-mermaid"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks into a <c>&lt;pre class="mermaid"&gt;…&lt;/pre&gt;</c>
/// container the mermaid runtime auto-discovers, and pulls the
/// runtime in via a head <c>&lt;script&gt;</c> tag.
/// </summary>
/// <remarks>
/// Implements <see cref="ICustomFenceHandler"/> so the
/// SuperFences dispatcher claims <c>language-mermaid</c> blocks
/// during its scan. Without SuperFences the plugin's own
/// <see cref="OnRenderPageAsync"/> pass walks the rendered HTML and
/// retags the same blocks; with SuperFences the retag becomes a
/// no-op because the dispatcher has already replaced them.
/// </remarks>
public sealed class MermaidPlugin : DocPluginBase, IHeadExtraProvider, ICustomFenceHandler
{
    /// <summary>UTF-8 head fragment that loads the mermaid runtime from a CDN and starts auto-discovery.</summary>
    private static readonly byte[] HeadFragment =
        [.. """
<script type="module">
import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs";
mermaid.initialize({ startOnLoad: true });
</script>
"""u8];

    /// <inheritdoc/>
    public override byte[] Name => "mermaid"u8.ToArray();

    /// <inheritdoc/>
    ReadOnlySpan<byte> ICustomFenceHandler.Language => "mermaid"u8;

    /// <inheritdoc/>
    public override ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var html = context.Html;
        var rendered = html.WrittenSpan;
        if (!MermaidRetagger.NeedsRetag(rendered))
        {
            return ValueTask.CompletedTask;
        }

        var rewritten = MermaidRetagger.Retag(rendered);
        html.ResetWrittenCount();
        html.Write(rewritten);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(HeadFragment);
    }

    /// <inheritdoc/>
    void ICustomFenceHandler.Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write("<pre class=\"mermaid\">"u8);
        writer.Write(content);
        writer.Write("</pre>"u8);
    }
}
