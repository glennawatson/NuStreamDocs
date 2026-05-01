// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Optimise;

/// <summary>Builder extension that registers <see cref="OptimisePlugin"/>.</summary>
public static class DocBuilderOptimiseExtensions
{
    /// <summary>Registers <see cref="OptimisePlugin"/> with default options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseOptimise(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new OptimisePlugin());
    }

    /// <summary>Registers <see cref="OptimisePlugin"/> with the supplied options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseOptimise(this DocBuilder builder, OptimiseOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new OptimisePlugin(options));
    }

    /// <summary>Registers <see cref="OptimisePlugin"/> with the supplied options and logger.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger to receive optimise diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseOptimise(this DocBuilder builder, OptimiseOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new OptimisePlugin(options, logger));
    }

    /// <summary>Registers <see cref="HtmlMinifyPlugin"/> with default options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseHtmlMinify(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new HtmlMinifyPlugin());
    }

    /// <summary>Registers <see cref="HtmlMinifyPlugin"/> with the supplied options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseHtmlMinify(this DocBuilder builder, HtmlMinifyOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new HtmlMinifyPlugin(options));
    }
}
