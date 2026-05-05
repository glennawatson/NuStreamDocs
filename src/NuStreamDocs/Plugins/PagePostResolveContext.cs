// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Per-page context handed to <see cref="IPagePostResolvePlugin.Rewrite"/>.
/// </summary>
/// <remarks>
/// Runs after the cross-page <see cref="IBuildResolvePlugin"/> barrier,
/// so <see cref="Html"/> can be rewritten using a frozen view of
/// cross-page state (e.g. resolved <c>@autoref:</c> markers, redirect
/// targets, privacy-rewritten asset URLs). Same ping-pong shape as
/// <see cref="PagePostRenderContext"/>: plugins return <c>false</c>
/// from <see cref="IPagePostResolvePlugin.NeedsRewrite"/> when there's
/// nothing to do for this page.
/// </remarks>
public readonly ref struct PagePostResolveContext
{
    /// <summary>Initializes a new instance of the <see cref="PagePostResolveContext"/> struct.</summary>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="html">UTF-8 HTML bytes produced by render and any prior post-resolve plugins.</param>
    /// <param name="output">UTF-8 sink the rewritten HTML is written into.</param>
    public PagePostResolveContext(FilePath relativePath, ReadOnlySpan<byte> html, IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        RelativePath = relativePath;
        Html = html;
        Output = output;
    }

    /// <summary>Gets the page path relative to the input root.</summary>
    public FilePath RelativePath { get; }

    /// <summary>Gets the UTF-8 HTML bytes to be rewritten.</summary>
    public ReadOnlySpan<byte> Html { get; }

    /// <summary>Gets the UTF-8 sink the rewritten HTML is written into.</summary>
    public IBufferWriter<byte> Output { get; }
}
