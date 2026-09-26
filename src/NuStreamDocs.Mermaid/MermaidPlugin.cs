// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Mermaid;

/// <summary>Renders <c>mermaid</c> fenced code blocks as mermaid diagrams and pulls in the mermaid runtime.</summary>
[System.Diagnostics.DebuggerDisplay("MermaidPlugin: {Name}")]
public sealed class MermaidPlugin : IPagePostRenderPlugin, IHeadExtraProvider, ICustomFenceHandler
{
    /// <summary>Configured options.</summary>
    private readonly MermaidOptions _options;

    /// <summary>Initializes a new instance of the <see cref="MermaidPlugin"/> class with default options.</summary>
    public MermaidPlugin()
        : this(MermaidOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MermaidPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public MermaidPlugin(MermaidOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "mermaid"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <summary>Gets the opening script tag that opts out of Cloudflare Rocket Loader.</summary>
    private static ReadOnlySpan<byte> CloudflareOptOutScriptOpen => "<script type=\"module\" data-cfasync=\"false\">\n"u8;

    /// <summary>Gets the plain opening script tag.</summary>
    private static ReadOnlySpan<byte> ScriptOpen => "<script type=\"module\">\n"u8;

    /// <summary>Gets the script body that loads the mermaid runtime and starts auto-discovery.</summary>
    private static ReadOnlySpan<byte> ScriptBody => """
           import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs";
           mermaid.initialize({ startOnLoad: true });
           </script>
           """u8;

    /// <inheritdoc/>
    ReadOnlySpan<byte> ICustomFenceHandler.Language => "mermaid"u8;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => MermaidRetagger.NeedsRetag(html);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PostRender(in PagePostRenderContext context) =>
        MermaidRetagger.Retag(context.Html, context.Output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        writer.Write(_options.CloudflareRocketLoaderOptOut ? CloudflareOptOutScriptOpen : ScriptOpen);
        writer.Write(ScriptBody);
    }

    /// <inheritdoc/>
    void ICustomFenceHandler.Render(ReadOnlySpan<byte> content, IBufferWriter<byte> writer)
    {
        writer.Write("<pre class=\"mermaid\">"u8);
        writer.Write(content);
        writer.Write("</pre>"u8);
    }
}
