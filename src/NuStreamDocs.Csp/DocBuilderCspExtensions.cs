// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Csp;

/// <summary>Builder-extension surface for <see cref="CspPlugin"/>.</summary>
public static class DocBuilderCspExtensions
{
    /// <summary>Registers <see cref="CspPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseCsp(this DocBuilder builder) => builder.UsePlugin(new CspPlugin());

    /// <summary>Registers <see cref="CspPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="CspOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseCsp(this DocBuilder builder, Func<CspOptions, CspOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.UsePlugin(new CspPlugin(configure(CspOptions.Default)));
    }

    /// <summary>Registers <see cref="CspPlugin"/> with caller-supplied options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseCsp(this DocBuilder builder, in CspOptions options) =>
        builder.UsePlugin(new CspPlugin(options));
}
