// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Post-render plugin that appends its marker to every page.</summary>
/// <param name="marker">UTF-8 marker appended after the rendered HTML.</param>
/// <param name="priority">Post-render ordering bid.</param>
internal sealed class MarkerAppendPlugin(byte[] marker, PluginPriority priority) : IPagePostRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "marker-append"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => priority;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => true;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        context.Output.Write(context.Html);
        context.Output.Write(marker);
    }
}
