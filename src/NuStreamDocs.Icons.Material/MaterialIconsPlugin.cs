// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Icons.Material;

/// <summary>
/// Plugin that contributes Google Material icon-font references to
/// every page's <c>&lt;head&gt;</c>.
/// </summary>
/// <remarks>
/// Writes UTF-8 bytes directly through the supplied
/// <see cref="IBufferWriter{T}"/>; option strings come from
/// dev-controlled configuration so they are emitted without HTML
/// encoding.
/// </remarks>
public sealed class MaterialIconsPlugin : DocPluginBase, IHeadExtraProvider
{
    /// <summary>Configured option set.</summary>
    private readonly MaterialIconsOptions _options;

    /// <summary>Initializes a new instance of the <see cref="MaterialIconsPlugin"/> class with default options.</summary>
    public MaterialIconsPlugin()
        : this(MaterialIconsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MaterialIconsPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public MaterialIconsPlugin(in MaterialIconsOptions options) => _options = options;

    /// <inheritdoc/>
    public override byte[] Name => "material-icons"u8.ToArray();

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var url = _options.ResolveStylesheetUrl();
        if (url.Length is 0)
        {
            return;
        }

        // Preconnect makes sense only when the URL actually points at
        // fonts.googleapis.com; a custom override URL probably points
        // somewhere else and the preconnect hint would be wasted.
        if (_options.Preconnect && url.AsSpan().IndexOf("fonts.googleapis.com"u8) >= 0)
        {
            HeadExtraWriter.WriteUtf8(writer, "<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">\n"u8);
            HeadExtraWriter.WriteUtf8(writer, "<link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>\n"u8);
        }

        HeadExtraWriter.WriteUtf8(writer, "<link rel=\"stylesheet\" href=\""u8);
        HeadExtraWriter.WriteUtf8(writer, url);
        HeadExtraWriter.WriteUtf8(writer, "\">\n"u8);
    }
}
