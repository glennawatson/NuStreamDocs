// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Transitions;

/// <summary>Builder-extension surface for <see cref="TransitionsPlugin"/>.</summary>
public static class DocBuilderTransitionsExtensions
{
    /// <summary>Registers <see cref="TransitionsPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseTransitions(this DocBuilder builder)
    {
        return builder.UsePlugin(new TransitionsPlugin());
    }

    /// <summary>Registers <see cref="TransitionsPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="TransitionsOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseTransitions(this DocBuilder builder, Func<TransitionsOptions, TransitionsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.UsePlugin(new TransitionsPlugin(configure(TransitionsOptions.Default)));
    }

    /// <summary>Registers <see cref="TransitionsPlugin"/> with caller-supplied options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseTransitions(this DocBuilder builder, in TransitionsOptions options)
    {
        return builder.UsePlugin(new TransitionsPlugin(options));
    }
}
