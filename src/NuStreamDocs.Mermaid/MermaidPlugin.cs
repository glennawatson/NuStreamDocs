// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Mermaid;

/// <summary>Renders <c>mermaid</c> fenced code blocks as mermaid diagrams and pulls in the mermaid runtime.</summary>
public sealed class MermaidPlugin : IPagePostRenderPlugin, IHeadExtraProvider, ICustomFenceHandler
{
    /// <summary>Head fragment that loads the mermaid runtime and starts auto-discovery.</summary>
    private static readonly byte[] HeadFragment =
        [.. """
<script type="module">
import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs";
mermaid.initialize({ startOnLoad: true });
</script>
"""u8];

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "mermaid"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    ReadOnlySpan<byte> ICustomFenceHandler.Language => "mermaid"u8;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => MermaidRetagger.NeedsRetag(html);

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context) =>
        MermaidRetagger.Retag(context.Html, context.Output);

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        writer.Write(HeadFragment);
    }

    /// <inheritdoc/>
    void ICustomFenceHandler.Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
    {
        writer.Write("<pre class=\"mermaid\">"u8);
        writer.Write(content);
        writer.Write("</pre>"u8);
    }
}
