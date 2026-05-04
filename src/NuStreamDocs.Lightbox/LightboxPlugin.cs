// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Lightbox;

/// <summary>
/// Plugin that pulls in glightbox for image lightbox behavior and
/// optionally wraps content images in lightbox triggers.
/// </summary>
public sealed class LightboxPlugin : DocPluginBase, IHeadExtraProvider
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
    public override byte[] Name => "lightbox"u8.ToArray();

    /// <inheritdoc/>
    public override ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_options.WrapImages)
        {
            return ValueTask.CompletedTask;
        }

        var html = context.Html;
        byte[] snapshot = [.. html.WrittenSpan];
        html.ResetWrittenCount();
        ImageWrapper.Rewrite(snapshot, _options.Selector, html);

        return ValueTask.CompletedTask;
    }

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
        HeadExtraWriter.WriteString(writer, _options.Selector);
        HeadExtraWriter.WriteUtf8(writer, "'});}});</script>\n"u8);
    }
}
