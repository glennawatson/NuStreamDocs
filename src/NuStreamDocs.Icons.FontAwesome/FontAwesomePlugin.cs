// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Icons.FontAwesome;

/// <summary>
/// Plugin that contributes a Font Awesome <c>&lt;link&gt;</c> stylesheet
/// reference to every page's <c>&lt;head&gt;</c>.
/// </summary>
/// <remarks>
/// Pure head-extras provider — no per-page work. Theme plugins
/// discover this plugin via <see cref="IHeadExtraProvider"/> during
/// configure and splice the returned bytes into their
/// <c>head_styles</c> partial.
/// <para>
/// Writes UTF-8 bytes directly through the supplied
/// <see cref="IBufferWriter{T}"/>; option strings come from
/// dev-controlled configuration so they are emitted without HTML
/// encoding.
/// </para>
/// </remarks>
public sealed class FontAwesomePlugin : IPlugin, IHeadExtraProvider
{
    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly FontAwesomeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="FontAwesomePlugin"/> class with default options.</summary>
    public FontAwesomePlugin()
        : this(FontAwesomeOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FontAwesomePlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public FontAwesomePlugin(in FontAwesomeOptions options) => _options = options;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "fontawesome"u8;

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (_options.StylesheetUrl.Length is 0)
        {
            return;
        }

        HeadExtraWriter.WriteUtf8(writer, "<link rel=\"stylesheet\" href=\""u8);
        HeadExtraWriter.WriteUtf8(writer, _options.StylesheetUrl);
        HeadExtraWriter.WriteUtf8(writer, "\""u8);
        HeadExtraWriter.AppendAttribute(writer, " crossorigin=\""u8, _options.Crossorigin);
        HeadExtraWriter.AppendAttribute(writer, " referrerpolicy=\""u8, _options.ReferrerPolicy);
        HeadExtraWriter.WriteUtf8(writer, ">\n"u8);
    }
}
