// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Fonts;

/// <summary>Builder-extension surface for <see cref="FontsPlugin"/>.</summary>
public static class DocBuilderFontsExtensions
{
    /// <summary>Registers <see cref="FontsPlugin"/> with the supplied font configuration.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options (the declared faces).</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseFonts(this DocBuilder builder, in FontsOptions options)
    {
        return builder.UsePlugin(new FontsPlugin(options));
    }

    /// <summary>Registers <see cref="FontsPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="FontsOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseFonts(this DocBuilder builder, Func<FontsOptions, FontsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.UsePlugin(new FontsPlugin(configure(FontsOptions.Default)));
    }

    /// <summary>Registers <see cref="FontsPlugin"/> with caller-supplied options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseFonts(this DocBuilder builder, in FontsOptions options, ILogger logger)
    {
        return builder.UsePlugin(new FontsPlugin(options, logger));
    }
}
