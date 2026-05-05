// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tests;

/// <summary>Test plugin that records the last page path it saw.</summary>
internal sealed class RecordingPlugin : IPagePostRenderPlugin
{
    /// <summary>Gets or sets the relative path of the most recent rendered page.</summary>
    public static string? LastPath { get; set; }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "recording"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => true;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        LastPath = context.RelativePath;
        context.Output.Write(context.Html);
    }
}
