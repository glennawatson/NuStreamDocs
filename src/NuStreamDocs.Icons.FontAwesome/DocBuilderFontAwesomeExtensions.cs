// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Icons.FontAwesome;

/// <summary>Builder-extension surface for <see cref="FontAwesomePlugin"/>.</summary>
public static class DocBuilderFontAwesomeExtensions
{
    /// <summary>Registers <see cref="FontAwesomePlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseFontAwesome(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new FontAwesomePlugin());
    }

    /// <summary>Registers <see cref="FontAwesomePlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="FontAwesomeOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseFontAwesome(this DocBuilder builder, Func<FontAwesomeOptions, FontAwesomeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(FontAwesomeOptions.Default);
        return builder.UsePlugin(new FontAwesomePlugin(options));
    }
}
