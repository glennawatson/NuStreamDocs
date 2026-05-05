// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Lightbox;

/// <summary>
/// Plugin that pulls in glightbox for image lightbox behavior and
/// optionally wraps content images in lightbox triggers.
/// </summary>
public sealed class LightboxPlugin : IPagePostRenderPlugin, IHeadExtraProvider
{
    /// <summary>Configured options.</summary>
    private readonly LightboxOptions _options;

    /// <summary>Initializes a new instance of the <see cref="LightboxPlugin"/> class with default options.</summary>
    public LightboxPlugin()
        : this(LightboxOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LightboxPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public LightboxPlugin(LightboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "lightbox"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Late);

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => _options.WrapImages && html.IndexOf("<img "u8) >= 0;

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context) =>
        ImageWrapper.Rewrite(context.Html, _options.Selector, context.Output);

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (_options.StylesheetUrl is [_, ..])
        {
            HeadExtraWriter.WriteUtf8(writer, "<link rel=\"stylesheet\" href=\""u8);
            HeadExtraWriter.WriteUtf8(writer, _options.StylesheetUrl);
            HeadExtraWriter.WriteUtf8(writer, "\">\n"u8);
        }

        if (_options.ScriptUrl is not [_, ..])
        {
            return;
        }

        HeadExtraWriter.WriteUtf8(writer, "<script defer src=\""u8);
        HeadExtraWriter.WriteUtf8(writer, _options.ScriptUrl);
        HeadExtraWriter.WriteUtf8(writer, "\"></script>\n"u8);
        HeadExtraWriter.WriteUtf8(writer, "<script>document.addEventListener('DOMContentLoaded',function(){if(window.GLightbox){GLightbox({selector:'."u8);
        HeadExtraWriter.WriteUtf8(writer, _options.Selector);
        HeadExtraWriter.WriteUtf8(writer, "'});}});</script>\n"u8);
    }
}
