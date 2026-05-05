// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Per-page context handed to <see cref="IPagePostRenderPlugin.PostRender"/>.
/// </summary>
/// <remarks>
/// Carries the rendered HTML bytes and a sink the rewritten HTML is
/// written into. The build pipeline ping-pongs two pooled buffers so
/// each post-render plugin sees a fresh writer; plugins that don't need
/// to rewrite return <c>false</c> from <see cref="IPagePostRenderPlugin.NeedsRewrite"/>
/// and the engine skips both the buffer swap and the call to
/// <see cref="IPagePostRenderPlugin.PostRender"/>.
/// </remarks>
public readonly ref struct PagePostRenderContext
{
    /// <summary>Initializes a new instance of the <see cref="PagePostRenderContext"/> struct.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="source">Original UTF-8 markdown bytes (for plugins that key behavior on source content).</param>
    /// <param name="html">UTF-8 HTML bytes produced by the renderer or the previous post-render plugin.</param>
    /// <param name="output">UTF-8 sink the rewritten HTML is written into.</param>
    public PagePostRenderContext(FilePath relativePath, ReadOnlySpan<byte> source, ReadOnlySpan<byte> html, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        RelativePath = relativePath;
        Source = source;
        Html = html;
        Output = output;
    }

    /// <summary>Gets the page path relative to the input root.</summary>
    public FilePath RelativePath { get; }

    /// <summary>Gets the original UTF-8 markdown bytes.</summary>
    public ReadOnlySpan<byte> Source { get; }

    /// <summary>Gets the UTF-8 HTML bytes to be rewritten.</summary>
    public ReadOnlySpan<byte> Html { get; }

    /// <summary>Gets the UTF-8 sink the rewritten HTML is written into.</summary>
    public IBufferWriter<byte> Output { get; }
}
