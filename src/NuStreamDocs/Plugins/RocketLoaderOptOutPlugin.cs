// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Html;

namespace NuStreamDocs.Plugins;

/// <summary>Opts every script on the finished page out of Cloudflare Rocket Loader.</summary>
[System.Diagnostics.DebuggerDisplay("RocketLoaderOptOutPlugin: {Name}")]
internal sealed class RocketLoaderOptOutPlugin : IPagePostRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "rocket-loader-opt-out"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, int.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => RocketLoaderOptOutRewriter.NeedsRewrite(html);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PostRender(in PagePostRenderContext context) =>
        RocketLoaderOptOutRewriter.Rewrite(context.Html, context.Output);
}
